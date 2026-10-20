// ============================================================================
// 文件名：AppServices.cs
// 功能描述：组合根（手工 DI），集中装配所有服务单例
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.IO;

/// <summary>
/// 应用服务定位器（组合根）
/// 手工构造单例，不引入 DI 框架，保持依赖最小化
/// </summary>
public static class AppServices
{
    private static bool _initialized;

    /// <summary>配置服务</summary>
    public static ConfigService Config { get; private set; } = null!;

    /// <summary>摸鱼历史服务</summary>
    public static MuyuHistoryService MuyuHistory { get; private set; } = null!;

    /// <summary>工资引擎</summary>
    public static SalaryEngine Engine { get; private set; } = null!;

    /// <summary>实时工资计时服务</summary>
    public static SalaryTickerService Ticker { get; private set; } = null!;

    /// <summary>摸鱼会话管理器</summary>
    public static MuyuSessionManager MuyuSessions { get; private set; } = null!;

    /// <summary>应用更新服务（唯一会发起网络请求的服务）</summary>
    public static UpdateService Update { get; private set; } = null!;

    /// <summary>下班到点提醒服务</summary>
    public static OffWorkReminderService OffWorkReminder { get; private set; } = null!;

    /// <summary>时间提供者（生产环境使用系统时间）</summary>
    public static TimeProvider TimeProvider { get; private set; } = TimeProvider.System;

    /// <summary>
    /// 应用数据目录：默认 %AppData%\SalaryTicker\；
    /// 可用 SALARYTICKER_DATA_DIR 覆盖（仅开发/截图用，避免读到或写回真实用户数据）。
    /// </summary>
    public static string AppDataDir { get; } =
        Environment.GetEnvironmentVariable("SALARYTICKER_DATA_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SalaryTicker");

    /// <summary>
    /// 初始化所有服务（应用启动时调用一次）
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        Config = new ConfigService(AppDataDir);
        MuyuHistory = new MuyuHistoryService(AppDataDir);
        Engine = new SalaryEngine(TimeProvider);
        Ticker = new SalaryTickerService(Engine, TimeProvider, () => Config.Settings);
        MuyuSessions = new MuyuSessionManager(MuyuHistory, Engine, TimeProvider, () => Config.Settings);
        Update = new UpdateService(TimeProvider);
        OffWorkReminder = new OffWorkReminderService(Ticker, () => Config.Settings);

        // 清理上次更新失败留下的临时文件
        UpdateService.CleanupTempFiles();

        _initialized = true;
    }

    /// <summary>
    /// 关闭所有服务（应用退出时调用）
    /// </summary>
    public static void Shutdown()
    {
        // 收尾进行中的摸鱼会话（标记为「中断」），避免这段计时在退出时凭空消失。
        // 必须在服务释放之前：它要用 Engine 算秒薪、写历史文件。
        MuyuSessions?.StopInterrupted();

        OffWorkReminder?.Dispose();
        Ticker?.Dispose();
        _initialized = false;
    }
}
