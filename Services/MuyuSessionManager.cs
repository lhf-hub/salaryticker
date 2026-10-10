// ============================================================================
// 文件名：MuyuSessionManager.cs
// 功能描述：摸鱼会话管理器（开始/结束/悬空会话处理）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Threading;

/// <summary>
/// 摸鱼会话
/// </summary>
public class MuyuSession
{
    public string ButtonId { get; set; } = "";
    public string Label { get; set; } = "";
    public DateTime StartTime { get; set; }
}

/// <summary>
/// 摸鱼会话管理器
/// </summary>
public sealed class MuyuSessionManager
{
    private readonly MuyuHistoryService _historyService;
    private readonly SalaryEngine _engine;
    private readonly TimeProvider _timeProvider;
    private readonly Func<AppSettings> _settingsProvider;
    private readonly Timer _liveUpdateTimer;
    private MuyuSession? _activeSession;

    /// <summary>
    /// 当前活动会话
    /// </summary>
    public MuyuSession? Active => _activeSession;

    /// <summary>
    /// 会话开始事件
    /// </summary>
    public event EventHandler<MuyuSession>? SessionStarted;

    /// <summary>
    /// 会话结束事件
    /// </summary>
    public event EventHandler<MuyuRecord>? SessionEnded;

    /// <summary>
    /// 会话实时更新事件（每秒触发）
    /// </summary>
    public event EventHandler<(MuyuSession session, TimeSpan elapsed, decimal cost)>? SessionUpdated;

    /// <summary>
    /// 初始化摸鱼会话管理器
    /// </summary>
    public MuyuSessionManager(MuyuHistoryService historyService, SalaryEngine engine, TimeProvider timeProvider, Func<AppSettings> settingsProvider)
    {
        _historyService = historyService;
        _engine = engine;
        _timeProvider = timeProvider;
        _settingsProvider = settingsProvider;
        _liveUpdateTimer = new Timer(OnLiveUpdate, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// 开始摸鱼会话
    /// </summary>
    public void Start(string buttonId, string label)
    {
        if (_activeSession != null) return;

        _activeSession = new MuyuSession
        {
            ButtonId = buttonId,
            Label = label,
            StartTime = _timeProvider.GetLocalNow().LocalDateTime
        };

        // 启动实时更新（每秒触发）
        _liveUpdateTimer.Change(1000, 1000);
        SessionStarted?.Invoke(this, _activeSession);
    }

    /// <summary>
    /// 结束摸鱼会话（用户手动点停）
    /// </summary>
    public MuyuRecord? Stop() => EndSession(interrupted: false, notify: true);

    /// <summary>
    /// 应用退出时收尾：把仍在进行的会话按「中断」落盘，避免这段计时凭空消失。
    /// 不触发 SessionEnded —— 界面正在关闭，没必要再更新一次。
    /// </summary>
    public MuyuRecord? StopInterrupted() => EndSession(interrupted: true, notify: false);

    /// <summary>
    /// 结束会话并落盘
    /// </summary>
    /// <param name="interrupted">是否因应用退出而中断</param>
    /// <param name="notify">是否触发 SessionEnded 事件</param>
    private MuyuRecord? EndSession(bool interrupted, bool notify)
    {
        if (_activeSession == null) return null;

        // 停止实时更新
        _liveUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);

        var endTime = _timeProvider.GetLocalNow().LocalDateTime;
        var duration = (int)(endTime - _activeSession.StartTime).TotalSeconds;

        // 退出收尾时，不足 1 秒的会话没有记录价值，直接丢弃
        // （否则每次刚点开就退出都会留下一条 0 秒记录）
        if (interrupted && duration <= 0)
        {
            _activeSession = null;
            return null;
        }

        // 获取当前秒薪
        var settings = GetCurrentSettings();
        var now = _timeProvider.GetLocalNow();
        var snapshot = _engine.ComputeSnapshot(now, settings);

        var record = new MuyuRecord
        {
            Date = DateOnly.FromDateTime(endTime),
            ButtonLabel = _activeSession.Label,
            StartTime = _activeSession.StartTime,
            EndTime = endTime,
            DurationSeconds = duration,
            CostAmount = duration * snapshot.SecondRate,
            Interrupted = interrupted
        };

        _historyService.Append(record);
        _activeSession = null;

        if (notify) SessionEnded?.Invoke(this, record);
        return record;
    }

    /// <summary>
    /// 恢复悬空会话（应用重启时）
    /// </summary>
    public void RecoverDangling(RuntimeState runtimeState, AppSettings settings)
    {
        if (runtimeState.MuyuStartTime == null) return;

        var startTime = runtimeState.MuyuStartTime.Value;
        var now = _timeProvider.GetLocalNow().LocalDateTime;

        // 如果跨天了，截断到昨日下班时间
        var today = DateOnly.FromDateTime(now);
        var startDay = DateOnly.FromDateTime(startTime);
        if (today > startDay)
        {
            // 跨天，截断到昨日 WorkEnd
            var yesterday = startDay.AddDays(-1);
            var workEnd = settings.Shift.WorkEnd;
            var workEndTimeOnly = TimeOnly.FromTimeSpan(workEnd);
            var truncatedEnd = yesterday.ToDateTime(workEndTimeOnly);
            if (startTime < truncatedEnd)
            {
                // 创建一条截断的记录
                var duration = (int)(truncatedEnd - startTime).TotalSeconds;
                if (duration > 0)
                {
                    var snapshot = _engine.ComputeSnapshot(new DateTimeOffset(startTime, TimeSpan.Zero), settings);
                    var record = new MuyuRecord
                    {
                        Date = startDay,
                        ButtonLabel = runtimeState.CurrentMuyuButtonId ?? "",
                        StartTime = startTime,
                        EndTime = truncatedEnd,
                        DurationSeconds = duration,
                        CostAmount = duration * snapshot.SecondRate
                    };
                    _historyService.Append(record);
                }
            }
            // 跨天后的部分不再恢复
        }
        else
        {
            // 同一天，恢复会话
            _activeSession = new MuyuSession
            {
                ButtonId = runtimeState.CurrentMuyuButtonId ?? "",
                Label = "",
                StartTime = startTime
            };
            _liveUpdateTimer.Change(1000, 1000);
            SessionStarted?.Invoke(this, _activeSession);
        }
    }

    /// <summary>
    /// 获取当前设置
    /// </summary>
    private AppSettings GetCurrentSettings() => _settingsProvider();

    /// <summary>
    /// 实时更新回调
    /// </summary>
    private void OnLiveUpdate(object? state)
    {
        if (_activeSession == null) return;

        var now = _timeProvider.GetLocalNow().LocalDateTime;
        var elapsed = now - _activeSession.StartTime;

        var settings = GetCurrentSettings();
        var snapshot = _engine.ComputeSnapshot(_timeProvider.GetLocalNow(), settings);
        var cost = (decimal)elapsed.TotalSeconds * snapshot.SecondRate;

        SessionUpdated?.Invoke(this, (_activeSession, elapsed, cost));
    }
}
