// ============================================================================
// 文件名：MuyuSessionManager.cs
// 功能描述：摸鱼会话管理器（开始/结束/悬空会话处理）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Collections.Generic;
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
/// 摸鱼实时状态
/// </summary>
/// <param name="Session">活动会话</param>
/// <param name="Elapsed">墙上时长（界面用来显示"摸了多久"，午休期间也继续走）</param>
/// <param name="WorkElapsed">落在工作时段内的时长（白赚的计算依据）</param>
/// <param name="Cost">白赚金额</param>
public record MuyuLiveUpdate(MuyuSession Session, TimeSpan Elapsed, TimeSpan WorkElapsed, decimal Cost);

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

    /// <summary>
    /// 保护 _activeSession 的锁。
    /// 定时器回调跑在线程池，会与 UI 线程的 Start/Stop 并发，
    /// 且回调自身可能重入（耗时超过 1 秒时）。
    /// </summary>
    private readonly object _gate = new();

    private MuyuSession? _activeSession;

    /// <summary>
    /// 当前活动会话
    /// </summary>
    public MuyuSession? Active
    {
        get { lock (_gate) return _activeSession; }
    }

    /// <summary>
    /// 会话开始事件
    /// </summary>
    public event EventHandler<MuyuSession>? SessionStarted;

    /// <summary>
    /// 会话结束事件。
    /// 负载为 null 表示该会话没有产生记录（全程落在非工作时段，或时长不足 1 秒），
    /// 仅用于通知界面复位——丢弃记录时也必须触发，否则按钮会卡在"进行中"。
    /// </summary>
    public event EventHandler<MuyuRecord?>? SessionEnded;

    /// <summary>
    /// 会话实时更新事件（每秒触发）
    /// </summary>
    public event EventHandler<MuyuLiveUpdate>? SessionUpdated;

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
        MuyuSession session;

        lock (_gate)
        {
            if (_activeSession != null) return;

            session = new MuyuSession
            {
                ButtonId = buttonId,
                Label = label,
                StartTime = _timeProvider.GetLocalNow().LocalDateTime
            };
            _activeSession = session;

            // 启动实时更新（每秒触发）
            _liveUpdateTimer.Change(1000, 1000);
        }

        SessionStarted?.Invoke(this, session);
    }

    /// <summary>
    /// 结束摸鱼会话（用户手动点停）
    /// </summary>
    public MuyuRecord? Stop() => EndSession(MuyuEndReason.Manual, notify: true);

    /// <summary>
    /// 应用退出时收尾：把仍在进行的会话按「中断」落盘，避免这段计时凭空消失。
    /// 不触发 SessionEnded —— 界面正在关闭，没必要再更新一次。
    /// </summary>
    public MuyuRecord? StopInterrupted() => EndSession(MuyuEndReason.AppExit, notify: false);

    /// <summary>
    /// 非工作时段自动中断：当前不在工作时段（午休/上班前/下班后/周末节假日）
    /// 且仍有活动会话时结束它，返回本次是否发生了中断。
    /// 公开是为了单元测试能直接驱动（理由同 GetLiveUpdate）。
    /// </summary>
    public bool InterruptIfNotEarning()
    {
        if (Active == null) return false;

        var settings = GetCurrentSettings();
        var snapshot = _engine.ComputeSnapshot(_timeProvider.GetLocalNow(), settings);
        if (snapshot.Status == WorkStatus.Earning) return false;

        EndSession(MuyuEndReason.Boundary, notify: true);
        return true;
    }

    /// <summary>
    /// 结束会话并落盘
    /// </summary>
    /// <param name="reason">结束方式</param>
    /// <param name="notify">是否触发 SessionEnded 事件</param>
    private MuyuRecord? EndSession(MuyuEndReason reason, bool notify)
    {
        MuyuSession session;

        // 先在锁内"认领"：置空并停表，保证并发调用只有一个能走到下面
        lock (_gate)
        {
            if (_activeSession == null) return null;

            session = _activeSession;
            _activeSession = null;
            _liveUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        var settings = GetCurrentSettings();
        var now = _timeProvider.GetLocalNow();
        var endTime = now.LocalDateTime;

        // 「白赚」只累计落在工作时段内的秒数，与「今日已赚」同口径：
        // 午休、上班前、下班后、周末节假日都不计
        var (holidays, makeupWorkDays) = GetDaySets(settings);
        var workSeconds = WorkTimeCalculator.GetWorkedSecondsBetween(
            session.StartTime, endTime, settings.Shift, holidays, makeupWorkDays);

        // 没有任何工作时段内的时长就没有记录价值 —— GetStats 是按"条数"计次的，
        // 落一条 0 秒会让"今日次数"+1。但仍然要通知界面复位，否则按钮会卡在
        // "进行中"且再点也救不回来（见 SessionEnded 的注释）。
        if (workSeconds <= 0)
        {
            if (notify) SessionEnded?.Invoke(this, null);
            return null;
        }

        // 获取当前秒薪
        var snapshot = _engine.ComputeSnapshot(now, settings);

        var record = new MuyuRecord
        {
            Date = DateOnly.FromDateTime(endTime),
            ButtonLabel = session.Label,
            StartTime = session.StartTime,
            EndTime = endTime,
            DurationSeconds = (int)workSeconds,
            CostAmount = workSeconds * snapshot.SecondRate,
            EndReason = reason
        };

        _historyService.Append(record);

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
    /// 构造节假日与补班日集合。
    /// 这里每秒重建一次是有意为之：SalaryEngine.ComputeSnapshot 每 tick 已经在做同样的
    /// 事（量级几微秒），而缓存要按 AppSettings 的对象引用失效，一旦将来有人就地改设置
    /// 就会静默失效，收益远小于风险。
    /// </summary>
    private static (HashSet<DateOnly> holidays, HashSet<DateOnly> makeupWorkDays) GetDaySets(AppSettings settings)
    {
        var holidays = new HashSet<DateOnly>();
        foreach (var h in settings.HolidayDates)
            holidays.Add(h.Date);

        var makeupWorkDays = new HashSet<DateOnly>();
        foreach (var m in settings.MakeupWorkDays)
            makeupWorkDays.Add(m.Date);

        return (holidays, makeupWorkDays);
    }

    /// <summary>
    /// 计算当前实时状态；没有活动会话时返回 null。
    /// 抽成公开方法是为了能在单元测试里直接驱动，不必依赖真实的 1 秒定时器。
    /// </summary>
    public MuyuLiveUpdate? GetLiveUpdate()
    {
        var session = Active;          // 锁内取一次，之后不再读字段
        if (session == null) return null;

        var now = _timeProvider.GetLocalNow();
        var settings = GetCurrentSettings();
        var (holidays, makeupWorkDays) = GetDaySets(settings);

        // 墙上时长：界面用它显示"已经摸了多久"
        var elapsed = now.LocalDateTime - session.StartTime;

        // 工作时段时长：白赚的计算依据
        var workSeconds = WorkTimeCalculator.GetWorkedSecondsBetween(
            session.StartTime, now.LocalDateTime, settings.Shift, holidays, makeupWorkDays);

        var snapshot = _engine.ComputeSnapshot(now, settings);

        return new MuyuLiveUpdate(
            session, elapsed, TimeSpan.FromSeconds(workSeconds), workSeconds * snapshot.SecondRate);
    }

    /// <summary>
    /// 实时更新回调（每秒）。
    /// 跑在线程池上，整段兜住异常 —— 未捕获异常会直接终止进程。
    /// </summary>
    private void OnLiveUpdate(object? state)
    {
        try
        {
            // 跨进午休或下班的那一刻自动中断
            if (InterruptIfNotEarning()) return;

            var live = GetLiveUpdate();
            if (live != null) SessionUpdated?.Invoke(this, live);
        }
        catch
        {
            // 定时器回调里绝不能把异常抛出去
        }
    }
}
