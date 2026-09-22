// ============================================================================
// 文件名：TaxCalculator.cs
// 功能描述：个税计算器（累计预扣法）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;

/// <summary>
/// 个税计算器（累计预扣法）
/// </summary>
public static class TaxCalculator
{
    /// <summary>
    /// 计算累计应纳税所得额
    /// </summary>
    public static decimal CumulativeTaxable(
        decimal grossMonthly,
        decimal insuranceMonthly,
        decimal specialDeduction,
        int months)
    {
        var monthlyTaxable = grossMonthly - 5000m - insuranceMonthly - specialDeduction;
        return Math.Max(0, monthlyTaxable) * months;
    }

    /// <summary>
    /// 计算累计应纳税额
    /// </summary>
    public static decimal CumulativeTax(decimal cumulativeTaxable, TaxParams taxParams)
    {
        if (cumulativeTaxable <= 0) return 0;
        var bracket = taxParams.BracketOf(cumulativeTaxable);
        return cumulativeTaxable * bracket.Rate - bracket.QuickDeduction;
    }

    /// <summary>
    /// 计算当月应预扣税额
    /// </summary>
    public static decimal ComputeMonthTax(
        decimal grossMonthly,
        decimal insuranceMonthly,
        decimal specialDeduction,
        int currentMonth,
        TaxParams taxParams)
    {
        if (currentMonth <= 1)
        {
            var cumulativeTaxable = CumulativeTaxable(grossMonthly, insuranceMonthly, specialDeduction, 1);
            return Round2(CumulativeTax(cumulativeTaxable, taxParams));
        }

        var currentCumulative = CumulativeTaxable(grossMonthly, insuranceMonthly, specialDeduction, currentMonth);
        var previousCumulative = CumulativeTaxable(grossMonthly, insuranceMonthly, specialDeduction, currentMonth - 1);

        var currentTax = CumulativeTax(currentCumulative, taxParams);
        var previousTax = CumulativeTax(previousCumulative, taxParams);

        return Round2(Math.Max(0, currentTax - previousTax));
    }

    public static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
