// ============================================================================
// 文件名：SalaryCalculator.cs
// 功能描述：工资计算器（组合五险一金 + 个税 → 税后到手）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;

/// <summary>
/// 工资计算器
/// </summary>
public static class SalaryCalculator
{
    /// <summary>
    /// 计算税后月薪结果
    /// </summary>
    public static SalaryResult Compute(AppSettings s)
    {
        if (s.Mode == SalaryMode.Net)
        {
            // 税后模式：不算五险一金和个税
            var dailyRate = s.NetSalary / s.Shift.MonthlyPayDays;
            var secondRate = dailyRate / s.Shift.DailyPayHours / 3600;
            return new SalaryResult(s.NetSalary, 0, 0, dailyRate, secondRate);
        }

        // 税前模式
        var insurance = SocialSecurityCalculator.ComputeAll(s);
        var monthTax = TaxCalculator.ComputeMonthTax(
            s.GrossSalary,
            insurance.Total,
            s.SpecialDeduction,
            s.CurrentMonth,
            s.Tax);

        var netMonthly = s.GrossSalary - insurance.Total - monthTax;
        if (netMonthly < 0) netMonthly = 0;

        var daily = netMonthly / s.Shift.MonthlyPayDays;
        var second = daily / s.Shift.DailyPayHours / 3600;

        return new SalaryResult(netMonthly, insurance.Total, monthTax, daily, second);
    }
}

/// <summary>
/// 工资计算结果
/// </summary>
public record SalaryResult(
    decimal NetMonthly,      // 税后月薪
    decimal InsuranceTotal,  // 五险一金合计
    decimal MonthTax,        // 当月个税
    decimal DailyRate,       // 日薪
    decimal SecondRate);     // 秒薪
