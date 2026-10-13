// ============================================================================
// 文件名：SettingsValidator.cs
// 功能描述：设置保存前的校验（纯函数，无副作用，便于单元测试）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System.Collections.Generic;

/// <summary>
/// 设置校验结果
/// </summary>
/// <param name="IsValid">是否通过</param>
/// <param name="Error">错误文案（通过时为空）</param>
/// <param name="PageIndex">
/// 出问题的那一页（见 <see cref="SettingsValidator"/> 的 Page* 常量；通过时为 -1）。
/// 保存失败时用它自动跳到对应设置页，用户不用自己找。
/// </param>
public record SettingsValidationResult(bool IsValid, string Error, int PageIndex)
{
    /// <summary>校验通过</summary>
    public static readonly SettingsValidationResult Ok = new(true, "", -1);
}

/// <summary>
/// 设置校验器。
/// 校验的是"构建好的草稿设置"，所以不依赖任何界面状态，可以直接单测。
/// </summary>
public static class SettingsValidator
{
    /// <summary>设置窗口左侧导航的页索引（与 SettingsWindow.xaml 的 ListBox 顺序一致）</summary>
    public const int PageSalary = 0;
    public const int PageTax = 1;
    public const int PageShift = 2;
    public const int PagePolicy = 4;

    /// <summary>
    /// 校验设置
    /// </summary>
    public static SettingsValidationResult Validate(AppSettings s)
    {
        var shift = ValidateShift(s);
        if (shift != null) return shift;

        var salary = ValidateSalary(s);
        if (salary != null) return salary;

        var tax = ValidateTax(s);
        if (tax != null) return tax;

        var policy = ValidatePolicy(s);
        if (policy != null) return policy;

        return SettingsValidationResult.Ok;
    }

    // ========== 班制设置 ==========

    private static SettingsValidationResult? ValidateShift(AppSettings s)
    {
        var shift = s.Shift;

        if (shift.WorkEnd <= shift.WorkStart)
            return Fail("下班时间必须晚于上班时间", PageShift);

        if (shift.DailyPayHours <= 0 || shift.DailyPayHours > 24)
            return Fail("日计薪小时必须在 0 到 24 之间", PageShift);

        foreach (var lunch in shift.LunchBreaks)
        {
            if (lunch.End <= lunch.Start)
                return Fail("午休的结束时间必须晚于开始时间", PageShift);

            // 午休落在上下班区间之外时，"工作区间"会被切成一段跨越下班的时间
            if (lunch.Start < shift.WorkStart || lunch.End > shift.WorkEnd)
                return Fail("午休时间必须落在上下班时间之内", PageShift);
        }

        return null;
    }

    // ========== 工资与五险一金 ==========

    private static SettingsValidationResult? ValidateSalary(AppSettings s)
    {
        if (s.Mode == SalaryMode.Gross && s.GrossSalary <= 0)
            return Fail("税前工资必须大于 0", PageSalary);

        if (s.Mode == SalaryMode.Net && s.NetSalary <= 0)
            return Fail("税后工资必须大于 0", PageSalary);

        if (s.PensionBase < 0 || s.PensionAmount < 0)
            return Fail("养老保险的基数与金额不能为负", PageSalary);

        if (s.MedicalBase < 0 || s.MedicalAmount < 0)
            return Fail("医疗保险的基数与金额不能为负", PageSalary);

        if (s.UnemploymentBase < 0 || s.UnemploymentAmount < 0)
            return Fail("失业保险的基数与金额不能为负", PageSalary);

        if (s.HousingFundBase < 0 || s.HousingFundAmount < 0 || s.HousingFundCap < 0)
            return Fail("公积金的基数、金额与上限不能为负", PageSalary);

        if (s.HousingFundRate < 0 || s.HousingFundRate > 1)
            return Fail("公积金比例必须在 0% 到 100% 之间", PageSalary);

        foreach (var d in s.OtherDeductions)
        {
            if (d.Amount < 0)
                return Fail($"其他扣款「{d.Name}」的金额不能为负", PageSalary);
        }

        return null;
    }

    // ========== 个税参数 ==========

    private static SettingsValidationResult? ValidateTax(AppSettings s)
    {
        if (s.SpecialDeduction < 0)
            return Fail("专项附加扣除不能为负", PageTax);

        if (s.MonthlyIncomes != null)
        {
            for (var i = 0; i < s.MonthlyIncomes.Count; i++)
            {
                // 留空的月份是合法的，按当前月薪算
                var income = s.MonthlyIncomes[i];
                if (income.HasValue && income.Value <= 0)
                    return Fail($"{i + 1} 月的税前收入必须大于 0（留空表示按当前月薪计算）", PageTax);
            }
        }

        return null;
    }

    // ========== 政策参数 ==========

    private static SettingsValidationResult? ValidatePolicy(AppSettings s)
    {
        var ss = s.SocialSecurity;

        if (RateOutOfRange(ss.PensionRate)) return Fail("养老保险比例必须在 0% 到 100% 之间", PagePolicy);
        if (RateOutOfRange(ss.MedicalTier1Rate)) return Fail("医保一档比例必须在 0% 到 100% 之间", PagePolicy);
        if (RateOutOfRange(ss.MedicalTier2Rate)) return Fail("医保二档比例必须在 0% 到 100% 之间", PagePolicy);
        if (RateOutOfRange(ss.UnemploymentRate)) return Fail("失业保险比例必须在 0% 到 100% 之间", PagePolicy);

        if (BadRange(ss.PensionMin, ss.PensionMax)) return Fail("养老保险的基数下限不能大于上限，且都不能为负", PagePolicy);
        if (BadRange(ss.MedicalMin, ss.MedicalMax)) return Fail("医疗保险的基数下限不能大于上限，且都不能为负", PagePolicy);
        if (BadRange(ss.UnemploymentMin, ss.UnemploymentMax)) return Fail("失业保险的基数下限不能大于上限，且都不能为负", PagePolicy);

        return null;
    }

    private static bool RateOutOfRange(decimal rate) => rate < 0 || rate > 1;

    private static bool BadRange(decimal min, decimal max) => min < 0 || max < 0 || min > max;

    private static SettingsValidationResult Fail(string error, int pageIndex) => new(false, error, pageIndex);
}
