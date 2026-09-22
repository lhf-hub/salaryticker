// 文件名：OtherDeduction.cs
// 描述：其他扣款项（如房租、餐补等）

namespace SalaryTicker.Models;

/// <summary>
/// 其他扣款项
/// </summary>
public class OtherDeduction
{
    /// <summary>名称</summary>
    public string Name { get; set; } = "";
    /// <summary>金额</summary>
    public decimal Amount { get; set; }
}
