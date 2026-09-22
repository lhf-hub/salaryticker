// ============================================================================
// 文件名：SocialSecurityCalculator.cs
// 功能描述：五险一金计算器（仅税前模式使用）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Linq;

/// <summary>
/// 五险一金计算器
/// </summary>
public static class SocialSecurityCalculator
{
    /// <summary>
    /// 通用单险种计算（三种输入模式统一入口）
    /// </summary>
    public static decimal CalcInsurance(
        InsuranceInputMode mode,
        decimal amountInput,
        decimal baseInput,
        decimal rate,
        decimal min,
        decimal max,
        decimal grossSalary)
    {
        return mode switch
        {
            InsuranceInputMode.Auto => Round2(ClampBase(grossSalary, min, max) * rate),
            InsuranceInputMode.MinBase => Round2(min * rate),
            InsuranceInputMode.MaxBase => Round2(max * rate),
            InsuranceInputMode.Base => Round2(ClampBase(baseInput, min, max) * rate),
            InsuranceInputMode.Amount => Math.Max(0, amountInput),
            _ => 0
        };
    }

    /// <summary>
    /// 计算养老保险
    /// </summary>
    public static decimal CalcPension(AppSettings s)
    {
        var ss = s.SocialSecurity;
        return CalcInsurance(
            s.PensionInputMode, s.PensionAmount, s.PensionBase,
            ss.PensionRate, ss.PensionMin, ss.PensionMax, s.GrossSalary);
    }

    /// <summary>
    /// 计算医疗保险
    /// </summary>
    public static decimal CalcMedical(AppSettings s)
    {
        var ss = s.SocialSecurity;
        var rate = s.MedicalTier == MedicalTier.Tier1 ? ss.MedicalTier1Rate : ss.MedicalTier2Rate;
        return CalcInsurance(
            s.MedicalInputMode, s.MedicalAmount, s.MedicalBase,
            rate, ss.MedicalMin, ss.MedicalMax, s.GrossSalary);
    }

    /// <summary>
    /// 计算失业保险
    /// </summary>
    public static decimal CalcUnemployment(AppSettings s)
    {
        var ss = s.SocialSecurity;
        return CalcInsurance(
            s.UnemploymentInputMode, s.UnemploymentAmount, s.UnemploymentBase,
            ss.UnemploymentRate, ss.UnemploymentMin, ss.UnemploymentMax, s.GrossSalary);
    }

    /// <summary>
    /// 计算公积金
    /// </summary>
    public static decimal CalcHousingFund(AppSettings s)
    {
        return s.HousingFundMode switch
        {
            HousingFundMode.Amount => Math.Max(0, s.HousingFundAmount),
            HousingFundMode.BaseRate => Round2(s.HousingFundBase * s.HousingFundRate),
            HousingFundMode.BaseRateCap => Round2(Math.Min(s.HousingFundBase, s.HousingFundCap) * s.HousingFundRate),
            _ => 0
        };
    }

    /// <summary>
    /// 计算五险一金明细
    /// </summary>
    public static InsuranceBreakdown ComputeAll(AppSettings s)
    {
        var pension = CalcPension(s);
        var medical = CalcMedical(s);
        var unemployment = CalcUnemployment(s);
        var housingFund = CalcHousingFund(s);
        var others = s.OtherDeductions?.Sum(d => d.Amount) ?? 0;
        var total = pension + medical + unemployment + housingFund + others;

        return new InsuranceBreakdown(pension, medical, unemployment, housingFund, s.OtherDeductions ?? new(), total);
    }

    private static decimal ClampBase(decimal value, decimal min, decimal max)
        => Math.Min(Math.Max(value, min), max);

    public static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// 五险一金明细记录
/// </summary>
public record InsuranceBreakdown(
    decimal Pension,
    decimal Medical,
    decimal Unemployment,
    decimal HousingFund,
    IReadOnlyList<OtherDeduction> Others,
    decimal Total);
