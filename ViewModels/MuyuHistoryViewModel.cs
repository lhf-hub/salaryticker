// ============================================================================
// 文件名：MuyuHistoryViewModel.cs
// 功能描述：摸鱼历史页面 ViewModel
// ============================================================================

namespace SalaryTicker.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SalaryTicker.Dialogs;
using SalaryTicker.Models;
using SalaryTicker.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

/// <summary>
/// 摸鱼历史记录行
/// </summary>
public class MuyuRecordRow
{
    public string DateText { get; set; } = "";
    public string ButtonLabel { get; set; } = "";
    public string TimeRangeText { get; set; } = "";
    public string DurationText { get; set; } = "";
    /// <summary>墙上时长与工作时段时长不一致时才填（悬停显示），一致时为 null</summary>
    public string? DurationTooltip { get; set; }
    public string CostText { get; set; } = "";
}

/// <summary>
/// 摸鱼历史 ViewModel
/// </summary>
public partial class MuyuHistoryViewModel : ObservableObject
{
    /// <summary>今日次数</summary>
    [ObservableProperty]
    private int _todayCount;

    /// <summary>今日总时长文本</summary>
    [ObservableProperty]
    private string _todayDurationText = "0 分钟";

    /// <summary>今日总白赚文本</summary>
    [ObservableProperty]
    private string _todayCostText = "¥0.00";

    /// <summary>本周次数</summary>
    [ObservableProperty]
    private int _weekCount;

    /// <summary>本周总时长文本</summary>
    [ObservableProperty]
    private string _weekDurationText = "0 分钟";

    /// <summary>本周总白赚文本</summary>
    [ObservableProperty]
    private string _weekCostText = "¥0.00";

    /// <summary>本月次数</summary>
    [ObservableProperty]
    private int _monthCount;

    /// <summary>本月总时长文本</summary>
    [ObservableProperty]
    private string _monthDurationText = "0 分钟";

    /// <summary>本月总白赚文本</summary>
    [ObservableProperty]
    private string _monthCostText = "¥0.00";

    /// <summary>幽默文案</summary>
    [ObservableProperty]
    private string _funnyText = "";

    /// <summary>历史记录列表</summary>
    public ObservableCollection<MuyuRecordRow> Records { get; } = new();

    /// <summary>是否启用摸鱼历史</summary>
    [ObservableProperty]
    private bool _isHistoryEnabled = true;

    public MuyuHistoryViewModel()
    {
        Refresh();
    }

    /// <summary>
    /// 刷新统计数据
    /// </summary>
    public void Refresh()
    {
        var service = AppServices.MuyuHistory;
        var settings = AppServices.Config.Settings;
        IsHistoryEnabled = settings.MuyuHistoryEnabled;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var stats = service.GetStats(today);

        TodayCount = stats.TodayCount;
        TodayDurationText = FormatDuration(stats.TodayTotalSeconds);
        TodayCostText = $"¥{stats.TodayTotalCost:N2}";

        WeekCount = stats.WeekCount;
        WeekDurationText = FormatDuration(stats.WeekTotalSeconds);
        WeekCostText = $"¥{stats.WeekTotalCost:N2}";

        MonthCount = stats.MonthCount;
        MonthDurationText = FormatDuration(stats.MonthTotalSeconds);
        MonthCostText = $"¥{stats.MonthTotalCost:N2}";

        FunnyText = GenerateFunnyText(stats.TodayCount, stats.TodayTotalCost);

        // 刷新记录列表（倒序）
        Records.Clear();
        foreach (var record in service.Records.OrderByDescending(r => r.StartTime).Take(100))
        {
            // DurationSeconds 只统计工作时段，与「时间段」列的墙上区间可能对不上，
            // 不一致时用悬停提示解释
            var wallSeconds = (int)(record.EndTime - record.StartTime).TotalSeconds;

            Records.Add(new MuyuRecordRow
            {
                DateText = record.Date.ToString("yyyy-MM-dd"),
                // 退出应用导致未正常点停的会话标成「中断」，避免看起来像一次完整摸鱼
                ButtonLabel = record.Interrupted
                    ? $"{record.ButtonLabel}（中断）"
                    : record.ButtonLabel,
                TimeRangeText = $"{record.StartTime:HH:mm} - {record.EndTime:HH:mm}",
                DurationText = FormatDuration(record.DurationSeconds),
                DurationTooltip = record.DurationSeconds == wallSeconds
                    ? null
                    : $"墙上 {FormatDuration(wallSeconds)} · 工作时段 {FormatDuration(record.DurationSeconds)}",
                CostText = $"¥{record.CostAmount:N2}"
            });
        }
    }

    /// <summary>
    /// 格式化时长
    /// </summary>
    private static string FormatDuration(int seconds)
    {
        if (seconds < 60) return $"{seconds} 秒";
        var minutes = seconds / 60;
        if (minutes < 60) return $"{minutes} 分钟";
        var hours = minutes / 60;
        return $"{hours} 小时 {minutes % 60} 分钟";
    }

    /// <summary>
    /// 生成幽默文案（按"白赚"金额分档，与实际购买力对应，避免出现"4 块钱买奶茶"这种失真）
    /// </summary>
    private static string GenerateFunnyText(int count, decimal cost)
    {
        if (count == 0)
            return "今天还没摸鱼，是个好员工！💼";

        // 按金额换算成真实可买到的东西（约 ¥15~20 一杯奶茶、¥25 一份下午茶、¥30~50 一顿午饭）
        var tail = cost switch
        {
            < 5m   => "一杯水的钱，但也是你的 💧",
            < 15m  => "够买瓶饮料了 🥤",
            < 30m  => "相当于老板请你喝了杯奶茶 🧋",
            < 60m  => "够买一份下午茶了 🍰",
            < 120m => "够一顿午饭了 🍜",
            _      => "可以请同事吃顿饭了 🍽️"
        };

        var fame = count >= 6 ? " 摸鱼界的劳模非你莫属！🏆" : "";

        return $"今天你已经摸鱼 {count} 次，白赚 ¥{cost:N2}，{tail}{fame}";
    }

    /// <summary>
    /// 清空历史（带确认）
    /// </summary>
    [RelayCommand]
    private void ClearAll()
    {
        var owner = Application.Current?.MainWindow;
        var confirmed = ConfirmDialog.Show(owner,
            "确定要清空所有摸鱼历史记录吗？此操作不可恢复。",
            "清空摸鱼历史",
            isDanger: true);

        if (!confirmed) return;

        AppServices.MuyuHistory.ClearAll();
        Refresh();
    }
}
