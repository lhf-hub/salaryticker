// ============================================================================
// 文件名：SalaryCalculatorTests.cs
// 功能描述：工资计算器基准测试（税前 20000、医保一档、公积金 5%）
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Services;
using SalaryTicker.Models;
using System.Collections.Generic;
using Xunit;

public class SalaryCalculatorTests
{
    private AppSettings CreateBaselineSettings()
    {
        return new AppSettings
        {
            Mode = SalaryMode.Gross,
            GrossSalary = 20000,
            PensionInputMode = InsuranceInputMode.Auto,
            MedicalTier = MedicalTier.Tier1,
            MedicalInputMode = InsuranceInputMode.Auto,
            UnemploymentInputMode = InsuranceInputMode.Auto,
            HousingFundMode = HousingFundMode.BaseRate,
            HousingFundBase = 20000,
            HousingFundRate = 0.05m,
            SpecialDeduction = 0,
            SocialSecurity = new SocialSecurityParams(),
            Tax = new TaxParams
            {
                Brackets = new()
                {
                    new() { UpperLimit = 36000, Rate = 0.03m, QuickDeduction = 0 },
                    new() { UpperLimit = 144000, Rate = 0.10m, QuickDeduction = 2520 },
                    new() { UpperLimit = 300000, Rate = 0.20m, QuickDeduction = 16920 },
                    new() { UpperLimit = 420000, Rate = 0.25m, QuickDeduction = 31920 },
                    new() { UpperLimit = 660000, Rate = 0.30m, QuickDeduction = 52920 },
                    new() { UpperLimit = 960000, Rate = 0.35m, QuickDeduction = 85920 },
                    new() { UpperLimit = null, Rate = 0.45m, QuickDeduction = 181920 }
                }
            },
            Shift = new ShiftSettings
            {
                MonthlyPayDays = 21.75m,
                DailyPayHours = 8m
            }
        };
    }

    [Fact]
    public void Compute_GrossMode_Baseline_Month9()
    {
        var settings = CreateBaselineSettings();
        // 2026 年 9 月实际计薪 22 天（22 个工作日 − 中秋 + 国庆补班）
        var result = SalaryCalculator.Compute(settings, payableDaysInMonth: 22, currentMonth: 9, currentYear: 2026);

        // 五险一金：养老 1600 + 医疗 400 + 失业 40 + 公积金 1000 = 3040
        Assert.Equal(3040m, result.InsuranceTotal);

        // 9月个税：累计 107640 → 10%-2520 = 8244；8月累计 95680 → 10%-2520 = 7048
        // 当月 = 8244 - 7048 = 1196
        Assert.Equal(1196m, result.MonthTax);

        // 税后 = 20000 - 3040 - 1196 = 15764
        Assert.Equal(15764m, result.NetMonthly);

        // 日薪 = 15764 / 22 = 716.5454...（按当月实际计薪天数摊平）
        Assert.Equal(15764m / 22m, result.DailyRate);

        // 秒薪 = 716.5454... / 8 / 3600
        Assert.Equal(15764m / 22m / 8m / 3600m, result.SecondRate);
    }

    [Fact]
    public void Compute_GrossMode_Month1_FirstBracket()
    {
        var settings = CreateBaselineSettings();
        var result = SalaryCalculator.Compute(settings, payableDaysInMonth: 21, currentMonth: 1, currentYear: 2026);

        // 1月个税：累计 11960 → 3% = 358.80
        Assert.Equal(358.80m, result.MonthTax);

        // 税后 = 20000 - 3040 - 358.80 = 16601.20
        Assert.Equal(16601.20m, result.NetMonthly);
    }

    // ========== 本年度各月税前收入（修正涨薪后的累计预扣偏差） ==========

    /// <summary>造一份"1 月至上月"的收入列表</summary>
    private static List<decimal?> Incomes(int months, decimal value)
    {
        var list = new List<decimal?>();
        for (var i = 0; i < months; i++) list.Add(value);
        return list;
    }

    [Fact]
    public void Compute_MonthlyIncomesSameAsSalary_MatchesUnfilled()
    {
        var settings = CreateBaselineSettings();
        var baseline = SalaryCalculator.Compute(settings, payableDaysInMonth: 22, currentMonth: 9, currentYear: 2026);

        settings.MonthlyIncomesYear = 2026;
        settings.MonthlyIncomes = Incomes(8, 20000m);   // 与当前月薪相同

        var filled = SalaryCalculator.Compute(settings, payableDaysInMonth: 22, currentMonth: 9, currentYear: 2026);

        Assert.Equal(baseline.MonthTax, filled.MonthTax);
        Assert.Equal(baseline.NetMonthly, filled.NetMonthly);
    }

    [Fact]
    public void Compute_MonthlyIncomesFromAnotherYear_IsIgnored()
    {
        var settings = CreateBaselineSettings();
        var baseline = SalaryCalculator.Compute(settings, payableDaysInMonth: 22, currentMonth: 9, currentYear: 2026);

        settings.MonthlyIncomesYear = 2025;             // 去年填的，不该被当成今年的
        settings.MonthlyIncomes = Incomes(8, 10000m);

        var result = SalaryCalculator.Compute(settings, payableDaysInMonth: 22, currentMonth: 9, currentYear: 2026);

        Assert.Equal(baseline.MonthTax, result.MonthTax);
    }

    [Fact]
    public void Compute_LowerEarlierMonths_ReducesCurrentMonthTax()
    {
        var settings = CreateBaselineSettings();
        var baseline = SalaryCalculator.Compute(settings, payableDaysInMonth: 22, currentMonth: 9, currentYear: 2026);

        settings.MonthlyIncomesYear = 2026;
        settings.MonthlyIncomes = Incomes(8, 10000m);   // 前 8 个月实际只有 10000

        var result = SalaryCalculator.Compute(settings, payableDaysInMonth: 22, currentMonth: 9, currentYear: 2026);

        // 累计被高估时个税会多算，收入填低后应当变少
        Assert.True(result.MonthTax < baseline.MonthTax,
            $"历史收入更低时当月个税应更少：填了 {result.MonthTax}，没填 {baseline.MonthTax}");
    }

    [Fact]
    public void Compute_PartiallyFilledMonths_OthersFallBackToCurrentSalary()
    {
        var filled = CreateBaselineSettings();
        filled.MonthlyIncomesYear = 2026;
        filled.MonthlyIncomes = Incomes(8, 20000m);

        // 只填了 1 月（值与当前月薪相同），其余留空 —— 结果应与"全填当前月薪"一致
        var partial = CreateBaselineSettings();
        partial.MonthlyIncomesYear = 2026;
        partial.MonthlyIncomes = new List<decimal?> { 20000m };

        var a = SalaryCalculator.Compute(filled, payableDaysInMonth: 22, currentMonth: 9, currentYear: 2026);
        var b = SalaryCalculator.Compute(partial, payableDaysInMonth: 22, currentMonth: 9, currentYear: 2026);

        Assert.Equal(a.MonthTax, b.MonthTax);
    }

    [Fact]
    public void Compute_ZeroPayableDays_FallsBackToMonthlyPayDays()
    {
        // 理论上不会出现（工作日本身至少一天），但要防除零
        var settings = CreateBaselineSettings();
        var result = SalaryCalculator.Compute(settings, payableDaysInMonth: 0, currentMonth: 9, currentYear: 2026);

        Assert.Equal(15764m / 21.75m, result.DailyRate);
    }

    [Fact]
    public void Compute_NonPositiveDivisors_DoesNotThrow()
    {
        // settings.json 被手改坏时也不应该每 tick 抛异常
        var settings = CreateBaselineSettings();
        settings.Shift.MonthlyPayDays = 0m;
        settings.Shift.DailyPayHours = 0m;

        var result = SalaryCalculator.Compute(settings, payableDaysInMonth: 0, currentMonth: 9, currentYear: 2026);

        Assert.True(result.DailyRate > 0m);
        Assert.True(result.SecondRate > 0m);
    }

    [Fact]
    public void Compute_NetMode_SkipsInsuranceAndTax()
    {
        var settings = new AppSettings
        {
            Mode = SalaryMode.Net,
            NetSalary = 15000,
            Shift = new ShiftSettings
            {
                MonthlyPayDays = 21.75m,
                DailyPayHours = 8m
            }
        };

        // 税后模式不算个税，月份不参与计算
        var result = SalaryCalculator.Compute(settings, payableDaysInMonth: 18, currentMonth: 1, currentYear: 2026);

        Assert.Equal(15000m, result.NetMonthly);
        Assert.Equal(0m, result.InsuranceTotal);
        Assert.Equal(0m, result.MonthTax);

        var expectedDaily = 15000m / 18m;
        Assert.Equal(expectedDaily, result.DailyRate);
    }

    [Fact]
    public void Compute_NegativeNetMonthly_ClampsToZero()
    {
        var settings = new AppSettings
        {
            Mode = SalaryMode.Gross,
            GrossSalary = 5000,  // 工资很低
            PensionInputMode = InsuranceInputMode.Amount,
            PensionAmount = 6000,  // 手动设置很高的五险一金
            MedicalInputMode = InsuranceInputMode.Auto,
            UnemploymentInputMode = InsuranceInputMode.Auto,
            HousingFundMode = HousingFundMode.Amount,
            HousingFundAmount = 0,
            SocialSecurity = new SocialSecurityParams(),
            Tax = new TaxParams
            {
                Brackets = new()
                {
                    new() { UpperLimit = 36000, Rate = 0.03m, QuickDeduction = 0 }
                }
            },
            Shift = new ShiftSettings
            {
                MonthlyPayDays = 21.75m,
                DailyPayHours = 8m
            }
        };

        var result = SalaryCalculator.Compute(settings, payableDaysInMonth: 21, currentMonth: 12, currentYear: 2026);
        Assert.Equal(0m, result.NetMonthly);  // 税后为负，钳制到 0
    }
}
