// 文件名：RuntimeState.cs
// 描述：运行时状态（不持久化，每次启动重置）

namespace SalaryTicker.Models;

/// <summary>
/// 运行时状态（内存中，不持久化）
/// </summary>
public class RuntimeState
{
    /// <summary>本月累计已赚</summary>
    public decimal MonthEarned { get; set; }
    /// <summary>今日已赚</summary>
    public decimal TodayEarned { get; set; }
    /// <summary>当前小时已赚（用于 Ticker 刷新）</summary>
    public decimal HourEarned { get; set; }
    /// <summary>上次 Tick 时间</summary>
    public DateTime LastTick { get; set; }
    /// <summary>是否在工作时段</summary>
    public bool IsWorking { get; set; }
    /// <summary>是否午休中</summary>
    public bool IsLunchBreak { get; set; }
    /// <summary>摸鱼开始时间（null = 未摸鱼）</summary>
    public DateTime? MuyuStartTime { get; set; }
    /// <summary>当前摸鱼按钮 ID</summary>
    public string? CurrentMuyuButtonId { get; set; }
}
