// 文件名：MuyuRecord.cs
// 描述：摸鱼历史记录

namespace SalaryTicker.Models;

/// <summary>
/// 单条摸鱼记录
/// </summary>
public class MuyuRecord
{
    /// <summary>日期</summary>
    public DateOnly Date { get; set; }
    /// <summary>按钮标签</summary>
    public string ButtonLabel { get; set; } = "";
    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; }
    /// <summary>结束时间</summary>
    public DateTime EndTime { get; set; }
    /// <summary>持续秒数</summary>
    public int DurationSeconds { get; set; }
    /// <summary>折算成本</summary>
    public decimal CostAmount { get; set; }

    /// <summary>
    /// 结束方式：手动点停 / 到点自动结束 / 应用退出中断。
    /// 老数据没有该字段，反序列化后为 <see cref="MuyuEndReason.Manual"/>，无需迁移。
    /// </summary>
    public MuyuEndReason EndReason { get; set; }
}
