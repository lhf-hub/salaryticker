// ============================================================================
// 文件名：ConfigService.cs
// 功能描述：配置服务门面，封装 settings.json 和 runtime.json 的读写
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.IO;

/// <summary>
/// 配置服务门面类
/// 负责管理应用设置（settings.json）和运行时状态（runtime.json）的持久化
/// </summary>
public sealed class ConfigService
{
    private readonly JsonFileStore<AppSettings> _settingsStore;
    private readonly JsonFileStore<RuntimeState> _runtimeStore;
    private AppSettings _settings;
    private DateTime _lastRuntimeSave = DateTime.MinValue;
    private readonly TimeSpan _runtimeSaveInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 当前应用设置
    /// </summary>
    public AppSettings Settings => _settings;

    /// <summary>
    /// 最后一次加载状态
    /// </summary>
    public LoadStatus LastLoadStatus { get; private set; }

    /// <summary>
    /// 最后一次加载消息
    /// </summary>
    public string? LastLoadMessage { get; private set; }

    /// <summary>
    /// 初始化配置服务
    /// </summary>
    /// <param name="appDataDir">应用数据目录</param>
    public ConfigService(string appDataDir)
    {
        var backupDir = Path.Combine(appDataDir, "backup");
        var options = JsonFileStore<AppSettings>.CreateDefaultOptions();

        _settingsStore = new JsonFileStore<AppSettings>(
            Path.Combine(appDataDir, "settings.json"), backupDir, options, SettingsDefaults.Create);
        _runtimeStore = new JsonFileStore<RuntimeState>(
            Path.Combine(appDataDir, "runtime.json"), backupDir, options);

        var result = _settingsStore.Load();
        _settings = result.Data;
        LastLoadStatus = result.Status;
        LastLoadMessage = result.Message;

        // 修正历史版本里写错的内置数据（用户配置优先于默认值，所以老用户不会
        // 自动跟着默认值变，需要在这里就地改对）。有改动立刻落盘。
        if (SettingsMigration.Apply(_settings))
        {
            SaveSettings();
        }
    }

    /// <summary>
    /// 保存当前设置到文件
    /// </summary>
    public void SaveSettings()
    {
        _settings.LastSaved = DateTime.Now;
        _settingsStore.Save(_settings);
    }

    /// <summary>
    /// 保存新的设置对象到文件
    /// </summary>
    /// <param name="newSettings">新的设置对象</param>
    public void SaveSettings(AppSettings newSettings)
    {
        _settings = newSettings;
        SaveSettings();
    }

    /// <summary>
    /// 加载运行时状态
    /// </summary>
    /// <returns>运行时状态对象</returns>
    public RuntimeState LoadRuntime()
    {
        var result = _runtimeStore.Load();
        return result.Data;
    }

    /// <summary>
    /// 节流保存运行时状态（15秒内只保存一次）
    /// </summary>
    /// <param name="state">运行时状态</param>
    public void SaveRuntimeThrottled(RuntimeState state)
    {
        var now = DateTime.Now;
        if (now - _lastRuntimeSave >= _runtimeSaveInterval)
        {
            _runtimeStore.Save(state);
            _lastRuntimeSave = now;
        }
    }

    /// <summary>
    /// 立即刷新运行时状态到文件（不节流）
    /// </summary>
    /// <param name="state">运行时状态</param>
    public void FlushRuntime(RuntimeState state)
    {
        _runtimeStore.Save(state);
        _lastRuntimeSave = DateTime.Now;
    }
}
