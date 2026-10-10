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

    // ========== 状态显示 ==========

    /// <summary>状态文案</summary>
    [ObservableProperty]
    private string _statusText = "正在加载…";

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

    // ========== 依赖 ==========

    /// <summary>设置窗口打开请求</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>迷你窗切换请求</summary>
    public event EventHandler? MiniWindowToggleRequested;

    /// <summary>退出请求</summary>
    public event EventHandler? ExitRequested;

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

        Status = snapshot.Status;
        StatusText = GetStatusText(snapshot.Status, snapshot.IsMuyu);
        UpdateCountdown(snapshot);

        // 「本月日薪」按当月实际计薪天数摊平，和上面的「本月已赚」同一口径
        SalarySummary = $"税后 ¥{snapshot.NetMonthly:N2} · 本月日薪 ¥{snapshot.DailyRate:N2} · 秒薪 ¥{snapshot.SecondRate:N4}";

        // 触发数字脉冲动画
        TriggerPulse();
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

        CountdownLabel = snapshot.Status == WorkStatus.NotStarted ? "距离上班" : "距离下班";
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
    private void OnMuyuUpdated(object? sender, (MuyuSession session, TimeSpan elapsed, decimal cost) args)
    {
        _dispatcher.InvokeAsync(() =>
        {
            var button = FindButton(args.session.ButtonId);
            if (button == null) return;

            button.IsRunning = true;
            button.IsActive = true;
            button.ElapsedText = $"{(int)args.elapsed.TotalMinutes:D2}:{args.elapsed.Seconds:D2}";
            button.CostText = $"¥{args.cost:N2}";

            IsMuyuRunning = true;
            MuyuStatusText = $"🐟 {args.session.Label} 中 · {button.ElapsedText} · 白赚 {button.CostText}";
        });
    }

    /// <summary>
    /// 摸鱼会话结束
    /// </summary>
    private void OnMuyuEnded(object? sender, MuyuRecord record)
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
