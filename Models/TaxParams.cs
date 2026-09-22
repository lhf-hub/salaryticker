// 文件名：TaxParams.cs
// 描述：个人所得税参数（7 级累进税率表）

namespace SalaryTicker.Models;

/// <summary>
/// 税率档位
/// </summary>
public class TaxBracket
{
    /// <summary>累计应纳税所得额上限（null = ∞）</summary>
    public decimal? UpperLimit { get; set; }
    /// <summary>税率</summary>
    public decimal Rate { get; set; }
    /// <summary>速算扣除数</summary>
    public decimal QuickDeduction { get; set; }
}

/// <summary>
/// 个税参数
/// </summary>
public class TaxParams
{
    /// <summary>基本减除费用（5000 元/月）</summary>
    public decimal BasicDeduction { get; set; } = 5000m;

    /// <summary>税率表（按 UpperLimit 升序）</summary>
    public List<TaxBracket> Brackets { get; set; } = new();

    /// <summary>
    /// 根据累计应纳税所得额返回对应税率档
    /// </summary>
    public TaxBracket BracketOf(decimal cumulativeTaxable)
    {
        foreach (var b in Brackets)
            if (b.UpperLimit == null || cumulativeTaxable <= b.UpperLimit)
                return b;
        return Brackets[^1];
    }
}
