// ============================================================================
// 文件名：SalaryCalculator.cs
// 功能描述：工资计算器（组合五险一金 + 个税 → 税后到手）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// 工资计算器
/// </summary>
public static class SalaryCalculator
{
    /// <summary>
    /// 计算税后月薪结果
    /// </summary>
    /// <param name="s">应用设置</param>
    /// <param name="payableDaysInMonth">
    /// 当月实际计薪天数（由 SalaryEngine 按工作日历算出），作为日薪/秒薪的除数。
    /// 按当月摊平后，整月累计正好等于税后月薪——否则节假日多的月份月底会短缺、
    /// 工作日多的月份会超出。
    /// </param>
    /// <param name="currentMonth">
    /// 当前月份（1-12），用于个税累计预扣。由 SalaryEngine 按系统日期传入，
    /// 不再作为用户设置持久化——跨月后自动跟随，不会停留在旧月份。
    /// </param>
    /// <param name="currentYear">
    /// 当前年份。用来判断 <see cref="AppSettings.MonthlyIncomes"/> 那份手工录入的
    /// 逐月收入是不是今年的——跨年后整份失效，不能拿去年填的值当今年的。
    /// </param>
    public static SalaryResult Compute(AppSettings s, int payableDaysInMonth, int currentMonth, int currentYear)
    {
        var payDays = ResolvePayDays(s, payableDaysInMonth);
        var payHours = ResolvePayHours(s.Shift);

        if (s.Mode == SalaryMode.Net)
        {
            // 税后模式：不算五险一金和个税
            var dailyRate = s.NetSalary / payDays;
            var secondRate = dailyRate / payHours / 3600;
            return new SalaryResult(s.NetSalary, 0, 0, dailyRate, secondRate);
        }

        // 税前模式
        var insurance = SocialSecurityCalculator.ComputeAll(s);
        var monthTax = TaxCalculator.ComputeMonthTax(
            BuildMonthlyTaxables(s, insurance.Total, currentMonth, currentYear), s.Tax);

        var netMonthly = s.GrossSalary - insurance.Total - monthTax;
        if (netMonthly < 0) netMonthly = 0;

        var daily = netMonthly / payDays;
        var second = daily / payHours / 3600;

        return new SalaryResult(netMonthly, insurance.Total, monthTax, daily, second);
    }

    /// <summary>
    /// 组装 1 月至当前月的逐月应纳税所得额。
    ///
    /// 未填的月份按当前税前月薪计算，所以"一次都不填"等价于原来的"假设每月工资相同"。
    /// 五险一金与专项附加扣除对所有月份取同一个值 —— 按用户当前的参数配置算出来，
    /// 不为历史月份单独推算（按下限/上限/自定义基数/直接填金额时，五险一金本来就是常数）。
    ///
    /// 手工录入的月份收入只在"数据属于今年"时生效；跨年后整份忽略。
    /// </summary>
    private static List<decimal> BuildMonthlyTaxables(
        AppSettings s, decimal insuranceMonthly, int currentMonth, int currentYear)
    {
        var result = new List<decimal>();
        if (currentMonth <= 0) return result;

        var incomes = s.MonthlyIncomes;
        var useManual = incomes != null
                        && incomes.Count > 0
                        && s.MonthlyIncomesYear == currentYear;

        for (var month = 1; month <= currentMonth; month++)
        {
            var gross = s.GrossSalary;
            if (useManual && month <= incomes!.Count && incomes[month - 1].HasValue)
                gross = incomes[month - 1]!.Value;

            result.Add(TaxCalculator.MonthlyTaxable(
                gross, insuranceMonthly, s.SpecialDeduction, s.Tax.BasicDeduction));
        }

        return result;
    }

    /// <summary>
    /// 日薪除数：优先用当月实际计薪天数；为 0 时退回设置的月计薪天数；仍非法则兜底 1
    /// （decimal 除零会抛异常，这里必须挡住）
    /// </summary>
    private static decimal ResolvePayDays(AppSettings s, int payableDaysInMonth)
    {
        if (payableDaysInMonth > 0) return payableDaysInMonth;
        return s.Shift.MonthlyPayDays > 0 ? s.Shift.MonthlyPayDays : 1m;
    }

    /// <summary>
    /// 日计薪小时数：非法时兜底 1（settings.json 被手改时不至于每 tick 抛异常）
    /// </summary>
    private static decimal ResolvePayHours(ShiftSettings shift)
        => shift.DailyPayHours > 0 ? shift.DailyPayHours : 1m;
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
