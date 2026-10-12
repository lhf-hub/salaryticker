// ============================================================================
// 文件名：TaxCalculatorTests.cs
// 功能描述：个税计算器单元测试
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Services;
using SalaryTicker.Models;
using System.Collections.Generic;
using Xunit;

public class TaxCalculatorTests
{
    private TaxParams CreateDefaultTaxParams()
    {
        return new TaxParams
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
        };
    }

    [Fact]
    public void CumulativeTaxable_SubtractsDeductions()
    {
        // 月应税 = 20000 - 5000 - 3040 = 11960
        var result = TaxCalculator.CumulativeTaxable(20000, 3040, 0, 1);
        Assert.Equal(11960m, result);
    }

    [Fact]
    public void CumulativeTax_Bracket1_3Percent()
    {
        var taxParams = CreateDefaultTaxParams();
        // 累计 11960 * 3% = 358.80
        var result = TaxCalculator.CumulativeTax(11960, taxParams);
        Assert.Equal(358.80m, result);
    }

    [Fact]
    public void ComputeMonthTax_Month1_FirstBracket()
    {
        var taxParams = CreateDefaultTaxParams();
        // 1月：累计 11960 → 3% → 358.80
        var result = TaxCalculator.ComputeMonthTax(20000, 3040, 0, 1, taxParams);
        Assert.Equal(358.80m, result);
    }

    [Fact]
    public void ComputeMonthTax_Month4_SecondBracket()
    {
        var taxParams = CreateDefaultTaxParams();
        // 4月：累计 47840 → 10%-2520 = 2264
        // 3月：累计 35880 → 3% = 1076.40
        // 当月 = 2264 - 1076.40 = 1187.60
        var result = TaxCalculator.ComputeMonthTax(20000, 3040, 0, 4, taxParams);
        Assert.Equal(1187.60m, result);
    }

    [Fact]
    public void ComputeMonthTax_NegativeTaxable_ReturnsZero()
    {
        var taxParams = CreateDefaultTaxParams();
        // 月应税 = 5000 - 5000 - 3040 = -3040 → 0
        var result = TaxCalculator.ComputeMonthTax(5000, 3040, 0, 1, taxParams);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void ComputeMonthTax_WithSpecialDeduction()
    {
        var taxParams = CreateDefaultTaxParams();
        // 专项附加 2000：月应税 = 20000 - 5000 - 3040 - 2000 = 9960
        // 累计 9960 * 3% = 298.80
        var result = TaxCalculator.ComputeMonthTax(20000, 3040, 2000, 1, taxParams);
        Assert.Equal(298.80m, result);
    }

    // ========== 逐月收入序列（修正"涨薪后按新工资回溯"的偏差） ==========

    [Fact]
    public void ComputeMonthTax_EmptySequence_ReturnsZero()
    {
        Assert.Equal(0m, TaxCalculator.ComputeMonthTax(new List<decimal>(), CreateDefaultTaxParams()));
    }

    [Fact]
    public void ComputeMonthTax_SameEveryMonth_MatchesLegacyOverload()
    {
        var taxParams = CreateDefaultTaxParams();
        var legacy = TaxCalculator.ComputeMonthTax(20000, 3040, 0, 4, taxParams);
        var bySequence = TaxCalculator.ComputeMonthTax(
            new List<decimal> { 11960m, 11960m, 11960m, 11960m }, taxParams);

        Assert.Equal(legacy, bySequence);
    }

    [Fact]
    public void ComputeMonthTax_AfterRaise_UsesActualMonthlyIncomes()
    {
        var taxParams = CreateDefaultTaxParams();
        // 前 9 个月月薪 15000、10 月涨到 30000（五险一金固定 3000/月）
        // 逐月应税 = 7000 × 9，然后 22000
        var actual = new List<decimal>();
        for (var i = 0; i < 9; i++) actual.Add(7000m);
        actual.Add(22000m);

        // 截至 9 月累计 63000 → 3780；截至 10 月 85000 → 5980；当月 = 2200
        Assert.Equal(2200m, TaxCalculator.ComputeMonthTax(actual, taxParams));
    }

    [Fact]
    public void ComputeMonthTax_AfterRaise_RetroactiveSalaryOverstatesTax()
    {
        var taxParams = CreateDefaultTaxParams();
        // 同一个 10 月，若拿新工资回溯前 9 个月：累计 198000 已跳到 20% 档 → 4400，
        // 比实际（2200）多算一倍
        var retroactive = new List<decimal>();
        for (var i = 0; i < 10; i++) retroactive.Add(22000m);

        Assert.Equal(4400m, TaxCalculator.ComputeMonthTax(retroactive, taxParams));
    }

    [Fact]
    public void ComputeMonthTax_RaiseWithinSameBracket_BothWaysAgree()
    {
        var taxParams = CreateDefaultTaxParams();
        // 15000 → 20000 没跨档（两边都是 10%），回溯与逐月填结果相同 ——
        // 这正是"只差几百块"体感的来源
        var actual = new List<decimal>();
        for (var i = 0; i < 9; i++) actual.Add(7000m);
        actual.Add(12000m);

        var retroactive = new List<decimal>();
        for (var i = 0; i < 10; i++) retroactive.Add(12000m);

        Assert.Equal(1200m, TaxCalculator.ComputeMonthTax(actual, taxParams));
        Assert.Equal(
            TaxCalculator.ComputeMonthTax(actual, taxParams),
            TaxCalculator.ComputeMonthTax(retroactive, taxParams));
    }
}
