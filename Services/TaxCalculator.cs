// ============================================================================
// 文件名：TaxCalculator.cs
// 功能描述：个税计算器（累计预扣法）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// 个税计算器（累计预扣法）
/// </summary>
public static class TaxCalculator
{
    /// <summary>
    /// 单月应纳税所得额（不足起征点按 0 计）
    /// </summary>
    public static decimal MonthlyTaxable(
        decimal grossMonthly,
        decimal insuranceMonthly,
        decimal specialDeduction,
        decimal basicDeduction)
        => Math.Max(0, grossMonthly - basicDeduction - insuranceMonthly - specialDeduction);

    /// <summary>
    /// 计算累计应纳税所得额（假设每月收入相同）
    /// </summary>
    public static decimal CumulativeTaxable(
        decimal grossMonthly,
        decimal insuranceMonthly,
        decimal specialDeduction,
        int months,
        decimal basicDeduction = 5000m)
        => MonthlyTaxable(grossMonthly, insuranceMonthly, specialDeduction, basicDeduction) * months;

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
    /// 按各月应纳税所得额计算当月应预扣税额（累计预扣法）。
    /// <paramref name="monthlyTaxables"/> 为 1 月至当前月的逐月应纳税所得额（调用方已 clamp 到 0）。
    ///
    /// 逐月传入而不是"月薪 × 月份数"，是为了支持年中涨薪：拿新工资回溯前几个月会把
    /// 累计高估，跨档时算出来的个税就比公司实际扣的多。
    /// </summary>
    public static decimal ComputeMonthTax(IReadOnlyList<decimal> monthlyTaxables, TaxParams taxParams)
    {
        if (monthlyTaxables == null || monthlyTaxables.Count == 0) return 0;

        var cumulative = 0m;
        var previousTax = 0m;

        for (var i = 0; i < monthlyTaxables.Count; i++)
        {
            cumulative += monthlyTaxables[i];
            var tax = CumulativeTax(cumulative, taxParams);

            // 最后一项：当月个税 = 累计应纳税额 − 截至上月的累计应纳税额
            if (i == monthlyTaxables.Count - 1)
                return Round2(Math.Max(0, tax - previousTax));

            previousTax = tax;
        }

        return 0;
    }

    /// <summary>
    /// 计算当月应预扣税额（假设每月收入相同）
    /// </summary>
    public static decimal ComputeMonthTax(
        decimal grossMonthly,
        decimal insuranceMonthly,
        decimal specialDeduction,
        int currentMonth,
        TaxParams taxParams)
    {
        if (currentMonth <= 0) return 0;

        var monthly = MonthlyTaxable(grossMonthly, insuranceMonthly, specialDeduction, taxParams.BasicDeduction);
        var monthlies = new decimal[currentMonth];
        for (var i = 0; i < currentMonth; i++) monthlies[i] = monthly;

        return ComputeMonthTax(monthlies, taxParams);
    }

    public static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
