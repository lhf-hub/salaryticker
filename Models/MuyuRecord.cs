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
    /// 是否为「中断」结束：应用退出时这段计时仍在进行，由退出流程收尾记录。
    /// 老数据没有该字段，反序列化后为 false，无需迁移。
    /// </summary>
    public bool Interrupted { get; set; }
}
