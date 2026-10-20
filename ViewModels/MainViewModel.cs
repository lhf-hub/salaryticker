// ============================================================================
// 文件名：MainViewModel.cs
// 功能描述：主界面 ViewModel（订阅实时快照，驱动界面刷新）
// ============================================================================

namespace SalaryTicker.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SalaryTicker.Models;
using SalaryTicker.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

/// <summary>
/// 主界面 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly Dispatcher _dispatcher;
    private DispatcherTimer? _pulseTimer;

    // ========== 金额显示 ==========

    /// <summary>本月已赚文本</summary>
    [ObservableProperty]
    private string _monthEarnedText = "¥0.00";

    /// <summary>今日已赚文本</summary>
    [ObservableProperty]
    private string _todayEarnedText = "¥0.00";

    /// <summary>当前小时已赚文本</summary>
    [ObservableProperty]
    private string _hourEarnedText = "¥0.00";

    /// <summary>本月进度（0-1）</summary>
    [ObservableProperty]
    private double _monthProgress;

    /// <summary>本月进度百分比文本</summary>
    [ObservableProperty]
    private string _monthProgressText = "0%";

    /// <summary>本月剩余工作日文本（不含今天；走完时给一句收尾话）</summary>
    [ObservableProperty]
    private string _remainingWorkDaysText = "";

    /// <summary>月份上下文（如「2026 年 10 月」）</summary>
    [ObservableProperty]
    private string _monthContextText = "";

    /// <summary>计薪天数上下文（如「18 个计薪日」）</summary>
    [ObservableProperty]
    private string _payDaysContextText = "";

    /// <summary>本月进度刻度（每格一个计薪日）</summary>
    public ObservableCollection<PayDayTick> PayDayTicks { get; } = new();

    /// <summary>离下一个假期还有多少天（或正在假期中）</summary>
    [ObservableProperty]
    private string _nextHolidayText = "正在计算…";

    // ========== 状态显示 ==========

    /// <summary>状态文案</summary>
    [ObservableProperty]
    private string _statusText = "正在加载…";

    /// <summary>状态短文案（台账行使用，不带 emoji/口号）</summary>
    [ObservableProperty]
    private string _statusShortText = "";

    /// <summary>当前是否处于计薪工作状态</summary>
    [ObservableProperty]
    private bool _isEarning;

    /// <summary>顶部实时状态文案（非工作时段显示当前状态）</summary>
    [ObservableProperty]
    private string _liveStatusText = "正在加载…";

    /// <summary>倒计时文本</summary>
    [ObservableProperty]
    private string _countdownText = "--:--:--";

    /// <summary>倒计时标签</summary>
    [ObservableProperty]
    private string _countdownLabel = "距离下班";

    /// <summary>工作状态</summary>
    [ObservableProperty]
    private WorkStatus _status = WorkStatus.NotStarted;

    /// <summary>工资计算摘要（如 "税后 ¥15,764.00 · 日薪 ¥724.78"）</summary>
    [ObservableProperty]
    private string _salarySummary = "";

    /// <summary>税后月薪文本（台账行）</summary>
    [ObservableProperty]
    private string _afterTaxMonthlyText = "¥0.00";

    /// <summary>日薪文本（台账行）</summary>
    [ObservableProperty]
    private string _dailyRateText = "¥0.00";

    /// <summary>秒薪文本（台账行）</summary>
    [ObservableProperty]
    private string _secondRateText = "¥0.0000";

    // ========== 动画与页面切换 ==========

    /// <summary>数字脉冲（每秒置 true，150ms 后复位）</summary>
    [ObservableProperty]
    private bool _isPulse;

    /// <summary>当前页面</summary>
    [ObservableProperty]
    private MainView _currentView = MainView.Dashboard;

    /// <summary>顶部提示条文本（配置损坏等）</summary>
    [ObservableProperty]
    private string _noticeText = "";

    /// <summary>是否显示提示条</summary>
    [ObservableProperty]
    private bool _isNoticeVisible;

    // ========== 摸鱼 ==========

    /// <summary>摸鱼按钮集合</summary>
    public ObservableCollection<MuyuButtonViewModel> MuyuButtons { get; } = new();

    /// <summary>摸鱼历史子 ViewModel（摸鱼统计页使用）</summary>
    public MuyuHistoryViewModel History { get; } = new();

    /// <summary>更新提示条子 ViewModel</summary>
    public UpdateViewModel Update { get; } = new();

    /// <summary>摸鱼按钮区是否可见（设置中可关闭）</summary>
    [ObservableProperty]
    private bool _isMuyuVisible = true;

    /// <summary>摸鱼状态文本（如 "🐟 摸鱼中 · 05:23 · 白赚 ¥7.55"）</summary>
    [ObservableProperty]
    private string _muyuStatusText = "";

    /// <summary>是否正在摸鱼</summary>
    [ObservableProperty]
    private bool _isMuyuRunning;

    /// <summary>
    /// 摸鱼按钮是否可用：只有工作时段能摸鱼。
    /// 午休、上班前、下班后、周末节假日都禁用（那段时间本来就归你，没有"摸鱼"一说）。
    /// </summary>
    [ObservableProperty]
    private bool _isMuyuEnabled;

    /// <summary>工作状态变化时同步摸鱼按钮可用性与禁用说明（单一数据源，不依赖 ApplySnapshot 的调用顺序）</summary>
    partial void OnStatusChanged(WorkStatus value)
    {
        IsEarning = value == WorkStatus.Earning;
        IsMuyuEnabled = value == WorkStatus.Earning;
        OnPropertyChanged(nameof(MuyuDisabledHint));
        OnPropertyChanged(nameof(MuyuDisabledShortHint));
    }

    /// <summary>不可摸鱼时展示的说明文案</summary>
    public string MuyuDisabledHint => Status switch
    {
        WorkStatus.LunchBreak => "午休时间不算摸鱼，这是你应得的休息 😴",
        WorkStatus.OffWork => "已经下班啦，这段时间完全属于你 👋",
        WorkStatus.ForcedRest => "今天休息，没有「摸鱼」这一说 🌴",
        _ => "还没到上班时间，先忙点自己的事吧 ☕"
    };

    /// <summary>不可摸鱼时的短说明（台账行使用，不带 emoji）</summary>
    public string MuyuDisabledShortHint => Status switch
    {
        WorkStatus.LunchBreak => "午休时间不算摸鱼，这是你应得的休息。",
        WorkStatus.OffWork => "已经下班，这段时间属于你。",
        WorkStatus.ForcedRest => "今天休息，不计工资，也没有摸鱼。",
        _ => "还没到上班时间，先忙自己的事。"
    };

    /// <summary>摸鱼中的短状态（台账行使用，不带 emoji）</summary>
    [ObservableProperty]
    private string _muyuStatusShortText = "";

    /// <summary>状态变化时一并刷新禁用文案</summary>
    partial void OnIsMuyuEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(MuyuDisabledHint));
        OnPropertyChanged(nameof(MuyuDisabledShortHint));
    }

    // ========== 依赖 ==========

    /// <summary>设置窗口打开请求</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>迷你窗切换请求</summary>
    public event EventHandler? MiniWindowToggleRequested;

    /// <summary>退出请求</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// 上次发起「运行中定期检查」的时间（内存态）。
    /// 用它而不是持久化的 LastUpdateCheckUtc，是为了让检查失败也能在下一个周期重试 ——
    /// 挂机多天时一次网络抖动不该让新版本提示推迟一整天。
    /// </summary>
    private DateTime _lastPeriodicCheckUtc = DateTime.MinValue;

    public MainViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        LoadMuyuButtons();
        SubscribeServices();
        LoadNotice();
    }

    /// <summary>
    /// 加载摸鱼按钮
    /// </summary>
    private void LoadMuyuButtons()
    {
        MuyuButtons.Clear();
        foreach (var button in AppServices.Config.Settings.MuyuButtons)
        {
            MuyuButtons.Add(new MuyuButtonViewModel(button));
        }
        IsMuyuVisible = MuyuButtons.Count > 0;
    }

    /// <summary>
    /// 订阅服务事件
    /// </summary>
    private void SubscribeServices()
    {
        AppServices.Ticker.Snapshot += OnSnapshot;
        AppServices.MuyuSessions.SessionUpdated += OnMuyuUpdated;
        AppServices.MuyuSessions.SessionEnded += OnMuyuEnded;
    }

    /// <summary>
    /// 加载提示信息
    /// </summary>
    private void LoadNotice()
    {
        var config = AppServices.Config;
        if (config.LastLoadStatus != LoadStatus.Ok && !string.IsNullOrEmpty(config.LastLoadMessage))
        {
            NoticeText = config.LastLoadMessage;
            IsNoticeVisible = true;
        }
    }

    /// <summary>
    /// 启动计时
    /// </summary>
    public void Start()
    {
        AppServices.Ticker.Start();
        AppServices.Ticker.RefreshNow();
    }

    /// <summary>
    /// 处理快照更新（后台线程 → 调度到 UI 线程）
    /// </summary>
    private void OnSnapshot(object? sender, SalarySnapshot snapshot)
    {
        _dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));
    }

    /// <summary>
    /// 应用快照到界面
    /// </summary>
    private void ApplySnapshot(SalarySnapshot snapshot)
    {
        MonthEarnedText = $"¥{snapshot.MonthEarned:N2}";
        TodayEarnedText = $"¥{snapshot.TodayEarned:N2}";
        HourEarnedText = $"¥{snapshot.HourEarned:N2}";
        MonthProgress = snapshot.MonthProgress;
        MonthProgressText = $"{snapshot.MonthProgress * 100:N1}%";
        RemainingWorkDaysText = $"{snapshot.RemainingWorkDays} 天";
        MonthContextText = $"{snapshot.Now.Year} 年 {snapshot.Now.Month} 月";
        PayDaysContextText = $"{snapshot.PayableDaysInMonth} 个计薪日";
        UpdatePayDayTicks(snapshot);
        NextHolidayText = NextHolidayCalculator.Describe(
            DateOnly.FromDateTime(snapshot.Now.DateTime),
            AppServices.Config.Settings.HolidayDates);

        Status = snapshot.Status;
        StatusText = GetStatusText(snapshot.Status, snapshot.IsMuyu);
        StatusShortText = GetStatusShortText(snapshot.Status, snapshot.IsMuyu);
        IsEarning = snapshot.Status == WorkStatus.Earning;
        LiveStatusText = IsEarning ? "实时计算中" : StatusShortText;
        UpdateCountdown(snapshot);

        // 「本月日薪」按当月实际计薪天数摊平，和上面的「本月已赚」同一口径
        SalarySummary = $"税后 ¥{snapshot.NetMonthly:N2} · 本月日薪 ¥{snapshot.DailyRate:N2} · 秒薪 ¥{snapshot.SecondRate:N4}";
        AfterTaxMonthlyText = $"¥{snapshot.NetMonthly:N2}";
        DailyRateText = $"¥{snapshot.DailyRate:N2}";
        SecondRateText = $"¥{snapshot.SecondRate:N4}";

        // 挂机时也要能收到新版本提示
        MaybeCheckUpdatePeriodically(snapshot.Now);

        // 只在计薪工作时段触发数字脉冲；休息/下班时不制造“还在涨”的错觉
        if (IsEarning) TriggerPulse();
    }

    /// <summary>
    /// 应用一直开着时定期检查更新。
    /// 真正是否发请求由 UpdateLogic.ShouldCheckNow 的最小间隔再兜一层，
    /// 所以这里每秒调用也不会刷请求。
    /// </summary>
    private void MaybeCheckUpdatePeriodically(DateTimeOffset now)
    {
        var nowUtc = now.UtcDateTime;
        if (!UpdateLogic.ShouldCheckPeriodically(_lastPeriodicCheckUtc, nowUtc)) return;

        _lastPeriodicCheckUtc = nowUtc;
        _ = Update.CheckForUpdateAsync();
    }

    /// <summary>
    /// 获取状态文案
    /// </summary>
    private static string GetStatusText(WorkStatus status, bool isMuyu)
    {
        if (isMuyu && status == WorkStatus.Earning)
            return "摸鱼中，老板没看见 👀";

        return status switch
        {
            WorkStatus.NotStarted => "还没开工，先喝杯水吧 ☕",
            WorkStatus.Earning => "赚钱中，每一秒都算数 💪",
            WorkStatus.LunchBreak => "午休中，好好休息 😴",
            WorkStatus.OffWork => "今天辛苦了，明天见 👋",
            WorkStatus.ForcedRest => "今天强制休息，不计工资 🌴",
            _ => ""
        };
    }

    /// <summary>
    /// 状态短文案（台账行使用，去掉 emoji 与口号）
    /// </summary>
    private static string GetStatusShortText(WorkStatus status, bool isMuyu)
    {
        if (isMuyu && status == WorkStatus.Earning)
            return "摸鱼中";

        return status switch
        {
            WorkStatus.NotStarted => "未上班",
            WorkStatus.Earning => "工作中",
            WorkStatus.LunchBreak => "午休",
            WorkStatus.OffWork => "已下班",
            WorkStatus.ForcedRest => "强制休息",
            _ => ""
        };
    }

    /// <summary>
    /// 更新本月进度刻度。格数 = 当月计薪天数；只在已填格数变化时重建集合，
    /// 避免每秒都触发 18 次 UI 更新。
    /// </summary>
    private void UpdatePayDayTicks(SalarySnapshot snapshot)
    {
        var total = snapshot.PayableDaysInMonth;
        var filled = total > 0
            ? Math.Clamp((int)Math.Floor(snapshot.MonthProgress * total), 0, total)
            : 0;

        if (PayDayTicks.Count == total && PayDayTicks.Count(t => t.IsFilled) == filled)
            return;

        PayDayTicks.Clear();
        for (var i = 0; i < total; i++)
            PayDayTicks.Add(new PayDayTick(i < filled));
    }

    /// <summary>
    /// 更新倒计时
    /// </summary>
    private void UpdateCountdown(SalarySnapshot snapshot)
    {
        if (snapshot.Countdown == null)
        {
            CountdownLabel = "";
            CountdownText = Status == WorkStatus.OffWork ? "已下班" : "";
            return;
        }

        CountdownLabel = snapshot.Status == WorkStatus.NotStarted ? "距上班" : "距下班";
        CountdownText = CountdownFormatter.Format(snapshot.Countdown.Value);
    }

    /// <summary>
    /// 触发数字脉冲动画（150ms）
    /// </summary>
    private void TriggerPulse()
    {
        IsPulse = true;
        _pulseTimer?.Stop();
        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _pulseTimer.Tick += (_, _) =>
        {
            IsPulse = false;
            _pulseTimer?.Stop();
        };
        _pulseTimer.Start();
    }

    // ========== 摸鱼事件 ==========

    /// <summary>
    /// 摸鱼会话实时更新
    /// </summary>
    private void OnMuyuUpdated(object? sender, MuyuLiveUpdate update)
    {
        _dispatcher.InvokeAsync(() =>
        {
            var button = FindButton(update.Session.ButtonId);
            if (button == null) return;

            button.IsRunning = true;
            button.IsActive = true;
            button.ElapsedText = $"{(int)update.Elapsed.TotalMinutes:D2}:{update.Elapsed.Seconds:D2}";
            button.CostText = $"¥{update.Cost:N2}";

            IsMuyuRunning = true;
            MuyuStatusText = $"🐟 {update.Session.Label} 中 · {button.ElapsedText} · 白赚 {button.CostText}";
            MuyuStatusShortText = $"{update.Session.Label}中，{button.ElapsedText}，白赚 {button.CostText}";
        });
    }

    /// <summary>
    /// 摸鱼会话结束（record 为 null 表示这段会话没有产生记录，只需复位界面）
    /// </summary>
    private void OnMuyuEnded(object? sender, MuyuRecord? record)
    {
        _dispatcher.InvokeAsync(() =>
        {
            foreach (var button in MuyuButtons)
            {
                button.IsRunning = false;
                button.IsActive = false;
                button.ElapsedText = "";
                button.CostText = "";
            }
            IsMuyuRunning = false;
            MuyuStatusText = "";
            MuyuStatusShortText = "";
        });
    }

    /// <summary>
    /// 查找按钮
    /// </summary>
    private MuyuButtonViewModel? FindButton(string buttonId)
    {
        foreach (var button in MuyuButtons)
        {
            if (button.Id == buttonId) return button;
        }
        return null;
    }

    // ========== 命令 ==========

    /// <summary>
    /// 点击摸鱼按钮（开始/结束切换）
    /// </summary>
    [RelayCommand]
    private void MuyuClick(MuyuButtonViewModel? button)
    {
        if (button == null) return;

        var sessions = AppServices.MuyuSessions;

        if (sessions.Active != null)
        {
            // 已有活动会话：结束它
            sessions.Stop();
            return;
        }

        // 开始新会话
        sessions.Start(button.Id, button.DisplayText);
        button.IsRunning = true;
        button.IsActive = true;
        IsMuyuRunning = true;
    }

    /// <summary>
    /// 打开设置
    /// </summary>
    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 切换迷你窗
    /// </summary>
    [RelayCommand]
    private void ToggleMiniWindow() => MiniWindowToggleRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 显示摸鱼历史
    /// </summary>
    [RelayCommand]
    private void ShowHistory()
    {
        History.Refresh();   // 进入页面时刷新统计数据
        CurrentView = MainView.MuyuHistory;
    }

    /// <summary>
    /// 返回仪表盘
    /// </summary>
    [RelayCommand]
    private void BackToDashboard() => CurrentView = MainView.Dashboard;

    /// <summary>
    /// 关闭提示条
    /// </summary>
    [RelayCommand]
    private void DismissNotice() => IsNoticeVisible = false;

    /// <summary>
    /// 退出应用
    /// </summary>
    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 重新加载设置（设置保存后调用）
    /// </summary>
    public void ReloadSettings()
    {
        LoadMuyuButtons();
        IsMuyuVisible = AppServices.Config.Settings.MuyuButtons.Count > 0;
    }
}
