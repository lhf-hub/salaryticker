// ============================================================================
// 文件名：SettingsValidatorTests.cs
// 功能描述：设置校验的单元测试（纯函数，不需要 WPF）
//           每条用例同时钉住"错误文案"与"该跳到哪一页"
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Models;
using SalaryTicker.Services;
using System;
using System.Collections.Generic;
using Xunit;

public class SettingsValidatorTests
{
    private static AppSettings CreateValid() => SettingsDefaults.Create();

    // ========== 基线 ==========

    [Fact]
    public void Validate_DefaultSettings_Passes()
    {
        var result = SettingsValidator.Validate(CreateValid());

        Assert.True(result.IsValid);
        Assert.Equal("", result.Error);
        Assert.Equal(-1, result.PageIndex);
    }

    // ========== 班制设置 ==========

    [Fact]
    public void Validate_WorkEndNotAfterStart_FailsOnShiftPage()
    {
        var s = CreateValid();
        s.Shift.WorkStart = new TimeSpan(18, 0, 0);
        s.Shift.WorkEnd = new TimeSpan(9, 0, 0);
        s.Shift.LunchBreaks = new List<LunchBreak>();

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("下班时间", result.Error);
        Assert.Equal(SettingsValidator.PageShift, result.PageIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24.1)]
    public void Validate_DailyPayHoursOutOfRange_FailsOnShiftPage(double hours)
    {
        var s = CreateValid();
        s.Shift.DailyPayHours = (decimal)hours;

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Equal(SettingsValidator.PageShift, result.PageIndex);
    }

    [Fact]
    public void Validate_LunchOutsideShift_FailsOnShiftPage()
    {
        var s = CreateValid();
        // 午休排到下班之后 —— 会把工作区间切成一段跨越下班的时间
        s.Shift.LunchBreaks = new List<LunchBreak>
        {
            new() { Start = new TimeSpan(19, 0, 0), End = new TimeSpan(20, 0, 0) }
        };

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("午休", result.Error);
        Assert.Equal(SettingsValidator.PageShift, result.PageIndex);
    }

    [Fact]
    public void Validate_LunchEndNotAfterStart_FailsOnShiftPage()
    {
        var s = CreateValid();
        s.Shift.LunchBreaks = new List<LunchBreak>
        {
            new() { Start = new TimeSpan(13, 0, 0), End = new TimeSpan(12, 0, 0) }
        };

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("午休", result.Error);
        Assert.Equal(SettingsValidator.PageShift, result.PageIndex);
    }

    // ========== 工资与五险一金 ==========

    [Fact]
    public void Validate_ZeroGrossSalary_FailsOnSalaryPage()
    {
        var s = CreateValid();
        s.Mode = SalaryMode.Gross;
        s.GrossSalary = 0;

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("税前工资", result.Error);
        Assert.Equal(SettingsValidator.PageSalary, result.PageIndex);
    }

    [Fact]
    public void Validate_NegativeInsuranceAmount_FailsOnSalaryPage()
    {
        var s = CreateValid();
        s.PensionInputMode = InsuranceInputMode.Amount;
        s.PensionAmount = -1;

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("养老", result.Error);
        Assert.Equal(SettingsValidator.PageSalary, result.PageIndex);
    }

    [Fact]
    public void Validate_HousingFundRateOver100Percent_FailsOnSalaryPage()
    {
        var s = CreateValid();
        s.HousingFundMode = HousingFundMode.BaseRate;
        s.HousingFundRate = 1.01m;

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("公积金比例", result.Error);
        Assert.Equal(SettingsValidator.PageSalary, result.PageIndex);
    }

    [Fact]
    public void Validate_NegativeOtherDeduction_FailsOnSalaryPage()
    {
        var s = CreateValid();
        s.OtherDeductions = new List<OtherDeduction> { new() { Name = "企业年金", Amount = -5 } };

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("企业年金", result.Error);
        Assert.Equal(SettingsValidator.PageSalary, result.PageIndex);
    }

    // ========== 个税参数 ==========

    [Fact]
    public void Validate_NegativeSpecialDeduction_FailsOnTaxPage()
    {
        var s = CreateValid();
        s.SpecialDeduction = -1;

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("专项附加扣除", result.Error);
        Assert.Equal(SettingsValidator.PageTax, result.PageIndex);
    }

    [Fact]
    public void Validate_ZeroMonthlyIncome_FailsOnTaxPage()
    {
        var s = CreateValid();
        s.MonthlyIncomesYear = DateTime.Now.Year;
        s.MonthlyIncomes = new List<decimal?> { 15000m, 0m };

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("2 月", result.Error);
        Assert.Equal(SettingsValidator.PageTax, result.PageIndex);
    }

    [Fact]
    public void Validate_NullMonthlyIncome_IsAllowed()
    {
        var s = CreateValid();
        s.MonthlyIncomesYear = DateTime.Now.Year;
        s.MonthlyIncomes = new List<decimal?> { 15000m, null, 12000m };

        Assert.True(SettingsValidator.Validate(s).IsValid);
    }

    // ========== 政策参数 ==========

    [Fact]
    public void Validate_InsuranceRateOver100Percent_FailsOnPolicyPage()
    {
        var s = CreateValid();
        s.SocialSecurity.PensionRate = 1.5m;

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("养老", result.Error);
        Assert.Equal(SettingsValidator.PagePolicy, result.PageIndex);
    }

    [Fact]
    public void Validate_InsuranceMinAboveMax_FailsOnPolicyPage()
    {
        var s = CreateValid();
        s.SocialSecurity.MedicalMin = 40000m;
        s.SocialSecurity.MedicalMax = 30000m;

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("医疗", result.Error);
        Assert.Equal(SettingsValidator.PagePolicy, result.PageIndex);
    }

    [Fact]
    public void Validate_NegativeInsuranceBase_FailsOnPolicyPage()
    {
        var s = CreateValid();
        s.SocialSecurity.UnemploymentMin = -1m;

        var result = SettingsValidator.Validate(s);

        Assert.False(result.IsValid);
        Assert.Contains("失业", result.Error);
        Assert.Equal(SettingsValidator.PagePolicy, result.PageIndex);
    }
}
