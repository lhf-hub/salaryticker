// 文件名：HolidayEntry.cs
// 描述：节假日条目

namespace SalaryTicker.Models;

/// <summary>
/// 单条节假日记录
/// </summary>
public class HolidayEntry
{
    /// <summary>日期</summary>
    public DateOnly Date { get; set; }
    /// <summary>节假日名称</summary>
    public string Name { get; set; } = "";
}
