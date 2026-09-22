// ============================================================================
// 文件名：UpdateService.cs
// 功能描述：应用更新服务（拉取版本清单 / 下载安装包 / SHA256 校验 / 接力替换自身）
//
// 说明：本项目其余部分完全离线，只有本文件会发起网络请求，且仅 GET 一个
//       几百字节的版本清单与（用户确认后的）新版本安装包，不上传任何数据。
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 应用更新服务
/// </summary>
public sealed class UpdateService
{
    private readonly TimeProvider _timeProvider;

    /// <summary>共享 HttpClient（连接超时 5 秒；整体超时按操作单独用 CancellationToken 控制）</summary>
    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>
    /// 清单反序列化选项：必须开启大小写不敏感
    /// （System.Text.Json 默认区分大小写，而清单用小写字段名如 "version"）
    /// </summary>
    public static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 初始化更新服务
    /// </summary>
    public UpdateService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>当前应用版本（如 "1.0.1"）</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>官方版本清单地址（设置中留空时使用）</summary>
    public const string DefaultManifestUrl = "https://salaryticker.leelab.com.cn/version.json";

    /// <summary>
    /// 解析实际使用的清单地址：设置为空时回退到官方地址
    /// （是否检查更新由"检查新版本"开关控制，而不是靠地址留空）
    /// </summary>
    public static string ResolveUrl(string? configuredUrl)
        => string.IsNullOrWhiteSpace(configuredUrl) ? DefaultManifestUrl : configuredUrl.Trim();

    /// <summary>更新包临时目录</summary>
    public static string UpdateDirectory => Path.Combine(Path.GetTempPath(), "SalaryTickerUpdate");

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan // 由调用方用 CancellationToken 控制
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"SalaryTicker/{CurrentVersion}");
        return client;
    }

    // ========================================================================
    // 一、拉取版本清单
    // ========================================================================

    /// <summary>
    /// 拉取并解析版本清单；任何失败（网络/超时/格式错误）均返回 null，不抛异常
    /// </summary>
    public async Task<UpdateManifest?> FetchManifestAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, ManifestJsonOptions, ct)
                .ConfigureAwait(false);

            // 清单必须包含版本号与下载地址，否则视为无效
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version) ||
                string.IsNullOrWhiteSpace(manifest.Url))
                return null;

            return manifest;
        }
        catch
        {
            // 离线 / 超时 / DNS 失败 / JSON 损坏 —— 一律静默降级
            return null;
        }
    }

    // ========================================================================
    // 二、下载并校验安装包
    // ========================================================================

    /// <summary>
    /// 下载新版安装包到临时目录并校验 SHA256
    /// </summary>
    /// <returns>下载成功返回新文件路径；失败抛 <see cref="InvalidOperationException"/>（含用户可读原因）</returns>
    public async Task<string> DownloadAsync(UpdateManifest manifest, IProgress<double>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(UpdateDirectory);
        var targetPath = Path.Combine(UpdateDirectory, $"SalaryTicker_{manifest.Version}_{Guid.NewGuid():N}.exe");

        try
        {
            using var response = await Http.GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? manifest.Size;

            await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var target = File.Create(targetPath))
            {
                var buffer = new byte[81920];
                long received = 0;
                int read;

                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    received += read;

                    if (totalBytes > 0)
                        progress?.Report((double)received / totalBytes);
                }
            }

            // 校验完整性：清单里给了哈希就必须匹配
            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var actual = await ComputeSha256Async(targetPath, ct).ConfigureAwait(false);
                if (!UpdateLogic.Sha256Matches(actual, manifest.Sha256))
                {
                    TryDelete(targetPath);
                    throw new InvalidOperationException("安装包校验失败（SHA256 不匹配），已取消更新。");
                }
            }

            progress?.Report(1.0);
            return targetPath;
        }
        catch (OperationCanceledException)
        {
            TryDelete(targetPath);
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            TryDelete(targetPath);
            throw new InvalidOperationException($"下载失败：{ex.Message}", ex);
        }
    }

    /// <summary>计算文件 SHA256（大写十六进制）</summary>
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    // ========================================================================
    // 三、接力替换（Windows 无法覆盖运行中的 exe，交由脚本在退出后完成）
    // ========================================================================

    /// <summary>
    /// 生成并启动接力脚本：等待本进程退出 → 覆盖 exe → 重新启动 → 自删除
    /// 调用方随后应立即退出应用
    /// </summary>
    public bool TryLaunchUpdater(string newExePath, out string error)
    {
        error = "";

        try
        {
            var targetExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
            {
                error = "无法定位当前程序路径。";
                return false;
            }

            if (!CanWriteToInstallDirectory())
            {
                error = "程序所在目录不可写入。请把 SalaryTicker.exe 移动到可写目录（如 D:\\Apps\\）后重试。";
                return false;
            }

            Directory.CreateDirectory(UpdateDirectory);
            var scriptPath = Path.Combine(UpdateDirectory, "apply-update.cmd");
            File.WriteAllText(scriptPath, RelayScript, System.Text.Encoding.ASCII);

            var psi = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add(targetExe);                    // %1 目标 exe
            psi.ArgumentList.Add(newExePath);                   // %2 新 exe
            psi.ArgumentList.Add(Environment.ProcessId.ToString()); // %3 当前 PID

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            error = $"无法启动更新程序：{ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 接力脚本：所有路径通过参数传入，脚本本身保持纯 ASCII（避免中文路径编码问题）
    /// </summary>
    private const string RelayScript =
        "@echo off\r\n" +
        "chcp 65001 >nul\r\n" +
        "set \"TARGET=%~1\"\r\n" +
        "set \"SOURCE=%~2\"\r\n" +
        "set \"OLDPID=%~3\"\r\n" +
        "\r\n" +
        "rem --- wait for the running instance to exit (max 60s) ---\r\n" +
        "set /a WAITED=0\r\n" +
        ":waitloop\r\n" +
        "tasklist /FI \"PID eq %OLDPID%\" /NH /FO CSV 2>nul | find \"%OLDPID%\" >nul\r\n" +
        "if errorlevel 1 goto replace\r\n" +
        "timeout /t 1 /nobreak >nul\r\n" +
        "set /a WAITED+=1\r\n" +
        "if %WAITED% GEQ 60 goto replace\r\n" +
        "goto waitloop\r\n" +
        "\r\n" +
        ":replace\r\n" +
        "rem --- overwrite the exe, retrying while the file is still locked (max 30 tries) ---\r\n" +
        "set /a TRIES=0\r\n" +
        ":tryloop\r\n" +
        "move /Y \"%SOURCE%\" \"%TARGET%\" >nul 2>&1\r\n" +
        "if not errorlevel 1 goto done\r\n" +
        "timeout /t 1 /nobreak >nul\r\n" +
        "set /a TRIES+=1\r\n" +
        "if %TRIES% GEQ 30 goto failed\r\n" +
        "goto tryloop\r\n" +
        "\r\n" +
        ":done\r\n" +
        "start \"\" \"%TARGET%\"\r\n" +
        "del \"%~f0\" >nul 2>&1\r\n" +
        "exit /b 0\r\n" +
        "\r\n" +
        ":failed\r\n" +
        "del \"%SOURCE%\" >nul 2>&1\r\n" +
        "del \"%~f0\" >nul 2>&1\r\n" +
        "exit /b 1\r\n";

    /// <summary>
    /// 检测程序所在目录是否可写（放在 Program Files 下会导致替换失败）
    /// </summary>
    public static bool CanWriteToInstallDirectory()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath)) return true;

            var dir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrWhiteSpace(dir)) return true;

            var probe = Path.Combine(dir, $".salaryticker-write-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>清理临时更新目录（启动时调用，清除上次失败留下的残留）</summary>
    public static void CleanupTempFiles()
    {
        try
        {
            if (!Directory.Exists(UpdateDirectory)) return;

            foreach (var file in Directory.GetFiles(UpdateDirectory))
            {
                // 保留正在使用的脚本（若有）—— 其余残留一律清理
                if (file.EndsWith("apply-update.cmd", StringComparison.OrdinalIgnoreCase)) continue;
                TryDelete(file);
            }
        }
        catch
        {
            // 清理失败不影响主流程
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 忽略
        }
    }
}
