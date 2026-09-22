// ============================================================================
// 文件名：SalaryCalculatorTests.cs
// 功能描述：工资计算器基准测试（税前 20000、医保一档、公积金 5%）
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Services;
using SalaryTicker.Models;
using Xunit;

public class SalaryCalculatorTests
{
    private AppSettings CreateBaselineSettings(int month = 9)
    {
        return new AppSettings
        {
            Mode = SalaryMode.Gross,
            GrossSalary = 20000,
            CurrentMonth = month,
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
        var settings = CreateBaselineSettings(9);
        var result = SalaryCalculator.Compute(settings);

        // 五险一金：养老 1600 + 医疗 400 + 失业 40 + 公积金 1000 = 3040
        Assert.Equal(3040m, result.InsuranceTotal);

        // 9月个税：累计 107640 → 10%-2520 = 8244；8月累计 95680 → 10%-2520 = 7048
        // 当月 = 8244 - 7048 = 1196
        Assert.Equal(1196m, result.MonthTax);

        // 税后 = 20000 - 3040 - 1196 = 15764
        Assert.Equal(15764m, result.NetMonthly);

        // 日薪 = 15764 / 21.75 = 724.7816...
        Assert.True(result.DailyRate > 724.78m && result.DailyRate < 724.79m);

        // 秒薪 = 724.7816 / 8 / 3600 = 0.025166...
        Assert.True(result.SecondRate > 0.02516m && result.SecondRate < 0.02517m);
    }

    [Fact]
    public void Compute_GrossMode_Month1_FirstBracket()
    {
        var settings = CreateBaselineSettings(1);
        var result = SalaryCalculator.Compute(settings);

        // 1月个税：累计 11960 → 3% = 358.80
        Assert.Equal(358.80m, result.MonthTax);

        // 税后 = 20000 - 3040 - 358.80 = 16601.20
        Assert.Equal(16601.20m, result.NetMonthly);
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

        var result = SalaryCalculator.Compute(settings);

        Assert.Equal(15000m, result.NetMonthly);
        Assert.Equal(0m, result.InsuranceTotal);
        Assert.Equal(0m, result.MonthTax);

        var expectedDaily = 15000m / 21.75m;
        Assert.Equal(expectedDaily, result.DailyRate);
    }

    [Fact]
    public void Compute_NegativeNetMonthly_ClampsToZero()
    {
        var settings = new AppSettings
        {
            Mode = SalaryMode.Gross,
            GrossSalary = 5000,  // 工资很低
            CurrentMonth = 12,
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

        var result = SalaryCalculator.Compute(settings);
        Assert.Equal(0m, result.NetMonthly);  // 税后为负，钳制到 0
    }
}
