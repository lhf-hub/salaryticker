// 文件名：ShiftSettings.cs
// 描述：班次设置（工作时间、午休、工作天数、大小周）

namespace SalaryTicker.Models;

/// <summary>
/// 午休时段
/// </summary>
public class LunchBreak
{
    /// <summary>开始时间</summary>
    public TimeSpan Start { get; set; }
    /// <summary>结束时间</summary>
    public TimeSpan End { get; set; }
}

/// <summary>
/// 班次设置
/// </summary>
public class ShiftSettings
{
    /// <summary>预设名称</summary>
    public string PresetName { get; set; } = "五天八小时双休";

    /// <summary>上班时间</summary>
    public TimeSpan WorkStart { get; set; } = new(9, 0, 0);
    /// <summary>下班时间</summary>
    public TimeSpan WorkEnd { get; set; } = new(18, 0, 0);

    /// <summary>午休时段列表</summary>
    public List<LunchBreak> LunchBreaks { get; set; } = new()
    {
        new() { Start = new(12, 0, 0), End = new(13, 0, 0) }
    };

    /// <summary>每周工作天</summary>
    public List<DayOfWeek> WorkDays { get; set; } = new()
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday
    };

    /// <summary>月计薪天数（制度）</summary>
    public decimal MonthlyPayDays { get; set; } = 21.75m;

    /// <summary>日计薪小时数</summary>
    public decimal DailyPayHours { get; set; } = 8m;

    // ========== 大小周扩展 ==========
    /// <summary>是否启用大小周</summary>
    public bool AlternateWeekEnabled { get; set; }

    /// <summary>小周工作天（通常含周六）</summary>
    public List<DayOfWeek> AltWorkDays { get; set; } = new()
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
    };

    /// <summary>锚点周的周一（用于推算大小周）</summary>
    public DateOnly AnchorWeekMonday { get; set; }

    /// <summary>锚点周是否大周</summary>
    public bool AnchorWeekIsBig { get; set; } = true;
}
