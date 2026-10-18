// ============================================================================
// 文件名：SalarySnapshot.cs
// 功能描述：工资快照（实时引擎每次 tick 的输出）
// ============================================================================

namespace SalaryTicker.Models;

using System;

/// <summary>
/// 工资快照
/// </summary>
public record SalarySnapshot
{
    /// <summary>当前时间</summary>
    public DateTimeOffset Now { get; init; }

    /// <summary>工作状态</summary>
    public WorkStatus Status { get; init; }

    /// <summary>是否在摸鱼</summary>
    public bool IsMuyu { get; init; }

    /// <summary>本月已赚</summary>
    public decimal MonthEarned { get; init; }

    /// <summary>今日已赚</summary>
    public decimal TodayEarned { get; init; }

    /// <summary>当前小时已赚</summary>
    public decimal HourEarned { get; init; }

    /// <summary>日薪（税后月薪 ÷ 当月实际计薪天数，月底累计正好等于月薪）</summary>
    public decimal DailyRate { get; init; }

    /// <summary>秒薪</summary>
    public decimal SecondRate { get; init; }

    /// <summary>税后月薪</summary>
    public decimal NetMonthly { get; init; }

    /// <summary>倒计时</summary>
    public TimeSpan? Countdown { get; init; }

    /// <summary>本月进度（0-1）</summary>
    public double MonthProgress { get; init; }

    /// <summary>当月实际计薪天数（日薪/秒薪的除数，也是进度刻度的格数）</summary>
    public int PayableDaysInMonth { get; init; }

    /// <summary>
    /// 本月剩余的工作日数（**不含今天**）。
    /// 今天算"正在上的这一天"而不是"还剩的一天"，所以这个数从早到晚不变，
    /// 只在跨过午夜时减一。
    /// </summary>
    public int RemainingWorkDays { get; init; }
}
