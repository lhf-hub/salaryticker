// ============================================================================
// 文件名：JsonFileStore.cs
// 功能描述：泛型 JSON 文件存储服务，支持原子写入、自动备份、损坏回退
// ============================================================================

namespace SalaryTicker.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 加载状态枚举
/// </summary>
public enum LoadStatus
{
    /// <summary>加载成功</summary>
    Ok,
    /// <summary>文件不存在，已创建默认值</summary>
    CreatedDefault,
    /// <summary>从备份恢复</summary>
    RestoredFromBackup,
    /// <summary>文件损坏，已重置为默认值</summary>
    CorruptResetToDefault
}

/// <summary>
/// 加载结果记录
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
/// <param name="Status">加载状态</param>
/// <param name="Data">加载的数据</param>
/// <param name="Message">状态消息</param>
/// <param name="RestoredFrom">恢复来源（备份文件路径）</param>
public sealed record LoadResult<T>(LoadStatus Status, T Data, string? Message, string? RestoredFrom) where T : class, new();

/// <summary>
/// 泛型 JSON 文件存储类
/// </summary>
/// <typeparam name="T">要存储的数据类型，必须是无参构造的引用类型</typeparam>
public sealed class JsonFileStore<T> where T : class, new()
{
    private readonly string _filePath;
    private readonly string _backupDir;
    private readonly JsonSerializerOptions _options;
    private readonly Func<T>? _defaultFactory;
    private const int MaxBackups = 10;

    /// <summary>
    /// 初始化 JSON 文件存储
    /// </summary>
    /// <param name="filePath">JSON 文件路径</param>
    /// <param name="backupDir">备份目录</param>
    /// <param name="options">JSON 序列化选项（可选）</param>
    /// <param name="defaultFactory">默认值工厂（可选，不传则使用 new T()）</param>
    public JsonFileStore(string filePath, string backupDir, JsonSerializerOptions? options = null, Func<T>? defaultFactory = null)
    {
        _filePath = filePath;
        _backupDir = backupDir;
        _options = options ?? CreateDefaultOptions();
        _defaultFactory = defaultFactory;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        Directory.CreateDirectory(_backupDir);
    }

    /// <summary>
    /// 创建默认数据实例
    /// </summary>
    private T CreateDefault() => _defaultFactory?.Invoke() ?? new T();

    /// <summary>
    /// 从文件加载数据
    /// </summary>
    /// <returns>加载结果，包含状态、数据和消息</returns>
    public LoadResult<T> Load()
    {
        // 1. 文件不存在 → 创建默认
        if (!File.Exists(_filePath))
        {
            var defaultData = CreateDefault();
            Save(defaultData);
            return new LoadResult<T>(LoadStatus.CreatedDefault, defaultData, "配置文件不存在，已创建默认值", null);
        }

        // 2. 尝试解析
        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<T>(json, _options);
            if (data == null) throw new JsonException("反序列化返回 null");
            return new LoadResult<T>(LoadStatus.Ok, data, null, null);
        }
        catch (Exception ex)
        {
            // 3. 解析失败 → 尝试备份
            var backupResult = TryRestoreFromBackup();
            if (backupResult != null)
            {
                return new LoadResult<T>(LoadStatus.RestoredFromBackup, backupResult.Value.data,
                    $"配置文件损坏，已从备份 {backupResult.Value.timestamp:yyyy-MM-dd HH:mm:ss} 恢复", backupResult.Value.file);
            }

            // 4. 全部失败 → 默认值
            var defaultData = new T();
            Save(defaultData);
            return new LoadResult<T>(LoadStatus.CorruptResetToDefault, defaultData,
                $"配置文件损坏且无可用备份，已重置为默认值。原始错误: {ex.Message}", null);
        }
    }

    /// <summary>
    /// 保存数据到文件
    /// </summary>
    /// <param name="data">要保存的数据</param>
    /// <returns>是否保存成功</returns>
    public bool Save(T data)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath)!;
            var tmpPath = Path.Combine(dir, Path.GetFileName(_filePath) + ".tmp");

            // 1. 写入临时文件
            var json = JsonSerializer.Serialize(data, _options);
            File.WriteAllText(tmpPath, json);

            // 2. 替换或移动到新位置
            if (File.Exists(_filePath))
            {
                File.Replace(tmpPath, _filePath, null);
            }
            else
            {
                File.Move(tmpPath, _filePath);
            }

            // 3. 保存成功后，备份新文件（确保备份总是最新的好版本）
            BackupCurrentFile();

            // 4. 清理旧备份
            CleanupOldBackups();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 备份当前文件
    /// </summary>
    private void BackupCurrentFile()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = Path.GetFileNameWithoutExtension(_filePath);
        var ext = Path.GetExtension(_filePath);
        var backupPath = Path.Combine(_backupDir, $"{fileName}_{timestamp}{ext}");
        File.Copy(_filePath, backupPath, overwrite: true);
    }

    /// <summary>
    /// 尝试从备份恢复
    /// </summary>
    /// <returns>恢复结果，包含数据、时间戳和备份文件路径；如果恢复失败返回 null</returns>
    private (T data, DateTime timestamp, string file)? TryRestoreFromBackup()
    {
        if (!Directory.Exists(_backupDir)) return null;

        var fileName = Path.GetFileNameWithoutExtension(_filePath);
        var ext = Path.GetExtension(_filePath);
        var pattern = $"{fileName}_*{ext}";

        var backups = Directory.GetFiles(_backupDir, pattern)
            .Select(f => new { File = f, Time = ExtractTimestamp(f) })
            .OrderByDescending(x => x.Time)
            .ToList();

        foreach (var backup in backups)
        {
            try
            {
                var json = File.ReadAllText(backup.File);
                var data = JsonSerializer.Deserialize<T>(json, _options);
                if (data != null)
                {
                    // 恢复为目标文件
                    Save(data);
                    return (data, backup.Time, backup.File);
                }
            }
            catch { /* 继续尝试下一个备份 */ }
        }
        return null;
    }

    /// <summary>
    /// 从文件名中提取时间戳
    /// </summary>
    /// <param name="filePath">备份文件路径</param>
    /// <returns>提取的时间戳；如果提取失败返回文件创建时间</returns>
    private DateTime ExtractTimestamp(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('_');
        if (parts.Length >= 2)
        {
            var datePart = parts[^2];
            var timePart = parts[^1];
            try
            {
                return DateTime.ParseExact($"{datePart}_{timePart}", "yyyyMMdd_HHmmss", null);
            }
            catch { }
        }
        return File.GetCreationTime(filePath);
    }

    /// <summary>
    /// 清理旧备份，保留最新的 MaxBackups 份
    /// </summary>
    private void CleanupOldBackups()
    {
        var fileName = Path.GetFileNameWithoutExtension(_filePath);
        var ext = Path.GetExtension(_filePath);
        var pattern = $"{fileName}_*{ext}";

        var backups = Directory.GetFiles(_backupDir, pattern)
            .Select(f => new { File = f, Time = ExtractTimestamp(f) })
            .OrderByDescending(x => x.Time)
            .Skip(MaxBackups)
            .ToList();

        foreach (var backup in backups)
        {
            try { File.Delete(backup.File); } catch { }
        }
    }

    /// <summary>
    /// 创建默认的 JSON 序列化选项
    /// </summary>
    /// <returns>配置好的序列化选项</returns>
    public static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter(), new Converters.TimeSpanJsonConverter() }
        };
    }
}
