// ============================================================================
// 文件名：UpdateViewModel.cs
// 功能描述：更新提示条状态机（检查 / 下载进度 / 立即更新 / 忽略此版本）
//
// 设计要点：不直接依赖 WPF（用 RestartRequested 事件通知宿主退出应用），
//           网络失败一律静默降级，绝不影响应用主功能。
// ============================================================================

namespace SalaryTicker.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SalaryTicker.Models;
using SalaryTicker.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 更新提示条 ViewModel
/// </summary>
public partial class UpdateViewModel : ObservableObject
{
    /// <summary>拉取清单的超时时间</summary>
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    /// <summary>下载安装包的超时时间</summary>
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    private readonly UpdateService _updateService;
    private readonly Func<AppSettings> _settingsAccessor;
    private readonly Action _saveSettings;

    private UpdateManifest? _pendingManifest;
    private CancellationTokenSource? _downloadCts;

    // ========== 对外状态 ==========

    /// <summary>提示条是否可见</summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>标题（如 "发现新版本 v1.1.0"）</summary>
    [ObservableProperty]
    private string _titleText = "";

    /// <summary>更新说明</summary>
    [ObservableProperty]
    private string _notesText = "";

    /// <summary>状态/错误文本</summary>
    [ObservableProperty]
    private string _statusText = "";

    /// <summary>是否正在下载</summary>
    [ObservableProperty]
    private bool _isDownloading;

    /// <summary>下载进度 0~1</summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>当前状态是否为错误（决定提示条配色）</summary>
    [ObservableProperty]
    private bool _isError;

    /// <summary>设置页"手动检查"的结果文本</summary>
    [ObservableProperty]
    private string _checkResultText = "";

    /// <summary>
    /// 最近一次手动检查是否失败。
    /// 失败时设置页会显示「在浏览器中打开官网」兜底入口 —— 更新通道连不上时，
    /// 用户仍可通过浏览器访问官网手动下载（该入口对服务器维护、网络异常等场景同样适用）。
    /// </summary>
    [ObservableProperty]
    private bool _hasCheckError;

    /// <summary>当前版本显示文本（如 "v1.1.0"）</summary>
    public string CurrentVersionText => $"v{UpdateService.CurrentVersion}";

    // ========== 程序位置与一键更新可用性 ==========

    /// <summary>程序所在目录是否可写（可写才能一键更新）</summary>
    public bool IsInstallDirWritable { get; }

    /// <summary>程序所在位置（设置页展示）</summary>
    public string InstallDirPath { get; }

    /// <summary>位置状态文本（设置页展示）</summary>
    public string InstallDirStatusText => IsInstallDirWritable
        ? "✅ 可写，支持一键更新"
        : "⚠️ 不可写，建议移到可写目录";

    /// <summary>是否支持一键更新（目录可写时）</summary>
    public bool CanAutoUpdate => IsInstallDirWritable;

    /// <summary>是否显示「立即更新」按钮</summary>
    public bool ShowAutoUpdateButton => CanAutoUpdate && !IsDownloading;

    /// <summary>是否显示「前往下载」按钮（目录不可写时的兜底）</summary>
    public bool ShowManualDownloadButton => !CanAutoUpdate && !IsDownloading;

    /// <summary>下载状态变化时同步按钮可见性</summary>
    partial void OnIsDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAutoUpdateButton));
        OnPropertyChanged(nameof(ShowManualDownloadButton));
    }

    /// <summary>是否需要展示"换个位置更好用"的小提示</summary>
    [ObservableProperty]
    private bool _isPortabilityNoticeVisible;

    /// <summary>小提示文案（语气友好，不用"错误/失败"）</summary>
    public string PortabilityNoticeText =>
        $"💡 小提示：把 SalaryTicker.exe 放到 D:\\Apps\\ 这类自己的文件夹里，就能享受一键更新啦～";

    /// <summary>本次会话内关闭该提示</summary>
    [RelayCommand]
    private void DismissPortabilityNotice() => IsPortabilityNoticeVisible = false;

    /// <summary>请求宿主重启（订阅方负责关闭应用，接力脚本会完成替换并重新启动）</summary>
    public event EventHandler? RestartRequested;

    public UpdateViewModel(
        UpdateService? updateService = null,
        Func<AppSettings>? settingsAccessor = null,
        Action? saveSettings = null)
    {
        _updateService = updateService ?? AppServices.Update;
        _settingsAccessor = settingsAccessor ?? (() => AppServices.Config.Settings);
        _saveSettings = saveSettings ?? (() => AppServices.Config.SaveSettings());

        // 启动时静默探测一次：可写就完全不打扰，不可写才提示（成本极低）
        IsInstallDirWritable = UpdateService.CanWriteToInstallDirectory();
        InstallDirPath = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "（未知）";
        IsPortabilityNoticeVisible = !IsInstallDirWritable;
    }

    // ========================================================================
    // 检查更新
    // ========================================================================

    /// <summary>
    /// 检查更新（异步、失败静默）
    /// </summary>
    /// <param name="force">true 为用户手动触发：跳过限流，并输出结果文本</param>
    /// <param name="urlOverride">手动检查时可传入设置页草稿中的地址（未保存也能生效）</param>
    public async Task CheckForUpdateAsync(bool force = false, string? urlOverride = null)
    {
        try
        {
            var settings = _settingsAccessor();
            var nowUtc = DateTime.UtcNow;

            // 留空则回退到官方地址；是否检查完全由"检查新版本"开关决定
            var url = UpdateService.ResolveUrl(
                string.IsNullOrWhiteSpace(urlOverride) ? settings.UpdateCheckUrl : urlOverride);

            if (!force && !UpdateLogic.ShouldCheckNow(
                    settings.UpdateCheckEnabled, url, settings.LastUpdateCheckUtc, nowUtc))
                return;

            if (force)
            {
                CheckResultText = "正在检查…";
                HasCheckError = false;
            }

            using var cts = new CancellationTokenSource(CheckTimeout);
            var manifest = await _updateService.FetchManifestAsync(url, cts.Token);

            // 无论成功与否都记录检查时间，起的是"最小间隔"作用（UpdateLogic.MinCheckInterval）：
            // 正常使用下每次进应用都会检查，只有刚查过的 10 分钟内再启动才会跳过。
            settings.LastUpdateCheckUtc = nowUtc;
            _saveSettings();

            if (manifest == null)
            {
                if (force)
                {
                    CheckResultText = "检查失败：无法连接更新服务器（请检查网络，或点右侧按钮到官网手动下载）";
                    HasCheckError = true;
                }
                return;
            }

            if (!UpdateLogic.ShouldNotify(UpdateService.CurrentVersion, manifest.Version, settings.SkippedVersion))
            {
                if (force) CheckResultText = $"当前已是最新版本（{CurrentVersionText}）";
                return;
            }

            _pendingManifest = manifest;
            TitleText = $"发现新版本 v{manifest.Version}";
            NotesText = manifest.ReleaseNotes;
            StatusText = "";
            IsError = false;
            IsVisible = true;

            if (force) CheckResultText = $"发现新版本 v{manifest.Version}，已在上方提示。";
        }
        catch (OperationCanceledException)
        {
            if (force)
            {
                CheckResultText = "检查超时，请稍后重试（也可点右侧按钮到官网手动下载）。";
                HasCheckError = true;
            }
        }
        catch
        {
            if (force)
            {
                CheckResultText = "检查失败，请稍后重试（也可点右侧按钮到官网手动下载）。";
                HasCheckError = true;
            }
        }
    }

    // ========================================================================
    // 立即更新
    // ========================================================================

    /// <summary>
    /// 下载并应用更新，随后请求宿主退出（接力脚本会完成替换与重启）
    /// </summary>
    [RelayCommand]
    private async Task StartUpdateAsync()
    {
        if (_pendingManifest == null || IsDownloading) return;

        IsDownloading = true;
        IsError = false;
        Progress = 0;
        StatusText = "正在下载…";

        try
        {
            _downloadCts = new CancellationTokenSource(DownloadTimeout);

            var progress = new Progress<double>(value =>
            {
                Progress = value;
                StatusText = $"正在下载… {value:P0}";
            });

            var newExePath = await _updateService.DownloadAsync(_pendingManifest, progress, _downloadCts.Token);

            StatusText = "下载完成，正在重启应用…";

            if (!_updateService.TryLaunchUpdater(newExePath, out var error))
            {
                IsError = true;
                StatusText = error;
                return;
            }

            // 通知宿主关闭应用；接力脚本检测到进程退出后替换 exe 并重新启动
            RestartRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            IsError = true;
            StatusText = "下载超时，已取消更新。";
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusText = ex.Message;
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    // ========================================================================
    // 忽略 / 关闭
    // ========================================================================

    /// <summary>忽略此版本（该版本不再提示，直到出现更高版本）</summary>
    [RelayCommand]
    private void SkipVersion()
    {
        if (_pendingManifest != null)
        {
            var settings = _settingsAccessor();
            settings.SkippedVersion = _pendingManifest.Version;
            _saveSettings();
        }
        IsVisible = false;
    }

    /// <summary>仅关闭本次提示（下次启动仍会提示）</summary>
    [RelayCommand]
    private void Dismiss() => IsVisible = false;

    /// <summary>
    /// 前往下载页（程序目录不可写时的兜底：用浏览器手动下载，替换 exe 即可）
    /// </summary>
    [RelayCommand]
    private void OpenDownloadPage()
    {
        var url = ResolveSiteUrl();
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 打开浏览器失败时静默处理，不打断用户
        }
    }

    /// <summary>
    /// 解析官网地址：优先用清单里的 siteUrl，其次取清单/设置地址的站点根
    /// </summary>
    public string ResolveSiteUrl()
    {
        if (_pendingManifest != null && !string.IsNullOrWhiteSpace(_pendingManifest.SiteUrl))
            return _pendingManifest.SiteUrl;

        var raw = _pendingManifest?.Url;
        if (string.IsNullOrWhiteSpace(raw)) raw = _settingsAccessor().UpdateCheckUrl;

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            return $"{uri.Scheme}://{uri.Authority}/";

        return "";
    }
}
