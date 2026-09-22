// ============================================================================
// 文件名：SocialSecurityCalculatorTests.cs
// 功能描述：五险一金计算器单元测试
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Services;
using SalaryTicker.Models;
using Xunit;

public class SocialSecurityCalculatorTests
{
    [Fact]
    public void CalcInsurance_Auto_ClampsAndMultiplies()
    {
        // 养老：基数=clamp(20000, 4775, 27549)=20000，比例 8% → 1600
        var result = SocialSecurityCalculator.CalcInsurance(
            InsuranceInputMode.Auto, 0, 0, 0.08m, 4775, 27549, 20000);
        Assert.Equal(1600m, result);
    }

    [Fact]
    public void CalcInsurance_Base_ClampsInputBase()
    {
        // 自定义基数 30000，被钳制到上限 27549 → 27549*0.08=2203.92
        var result = SocialSecurityCalculator.CalcInsurance(
            InsuranceInputMode.Base, 0, 30000, 0.08m, 4775, 27549, 0);
        Assert.Equal(2203.92m, result);
    }

    [Fact]
    public void CalcInsurance_MinBase_UsesLowerLimit()
    {
        // 公司按最低基数申报：工资 20000，仍按下限 4775 计算 → 4775*0.08 = 382
        var result = SocialSecurityCalculator.CalcInsurance(
            InsuranceInputMode.MinBase, 0, 0, 0.08m, 4775, 27549, 20000);
        Assert.Equal(382m, result);
    }

    [Fact]
    public void CalcInsurance_MaxBase_UsesUpperLimit()
    {
        // 按上限申报：工资 20000，按上限 27549 计算 → 27549*0.08 = 2203.92
        var result = SocialSecurityCalculator.CalcInsurance(
            InsuranceInputMode.MaxBase, 0, 0, 0.08m, 4775, 27549, 20000);
        Assert.Equal(2203.92m, result);
    }

    [Fact]
    public void ComputeAll_MinBaseCompany_OnlyMedicalAtActualWage()
    {
        // 典型场景：公司除医保外都按最低基数申报，医保按实际工资
        var settings = new AppSettings
        {
            GrossSalary = 20000,
            PensionInputMode = InsuranceInputMode.MinBase,      // 按下限
            MedicalTier = MedicalTier.Tier1,
            MedicalInputMode = InsuranceInputMode.Auto,          // 按工资
            UnemploymentInputMode = InsuranceInputMode.MinBase,  // 按下限
            HousingFundMode = HousingFundMode.BaseRate,
            HousingFundBase = 20000,
            HousingFundRate = 0.05m,
            SocialSecurity = new SocialSecurityParams()
        };

        var breakdown = SocialSecurityCalculator.ComputeAll(settings);

        Assert.Equal(382m, breakdown.Pension);       // 4775 × 8%
        Assert.Equal(400m, breakdown.Medical);       // 20000 × 2%（按实际工资）
        Assert.Equal(5.04m, breakdown.Unemployment); // 2520 × 0.2%
        Assert.Equal(1000m, breakdown.HousingFund);  // 20000 × 5%
        Assert.Equal(1787.04m, breakdown.Total);
    }

    [Fact]
    public void CalcInsurance_Amount_ReturnsDirectValue()
    {
        var result = SocialSecurityCalculator.CalcInsurance(
            InsuranceInputMode.Amount, 500, 0, 0.08m, 4775, 27549, 0);
        Assert.Equal(500m, result);
    }

    [Fact]
    public void CalcInsurance_LowGross_ClampsToMinimum()
    {
        // 工资 3000 < 下限 4775 → 基数钳制到 4775 → 4775*0.08=382
        var result = SocialSecurityCalculator.CalcInsurance(
            InsuranceInputMode.Auto, 0, 0, 0.08m, 4775, 27549, 3000);
        Assert.Equal(382m, result);
    }

    [Fact]
    public void CalcMedical_Tier1_UsesTier1Rate()
    {
        var settings = new AppSettings
        {
            GrossSalary = 20000,
            MedicalTier = MedicalTier.Tier1,
            MedicalInputMode = InsuranceInputMode.Auto,
            SocialSecurity = new SocialSecurityParams()
        };
        var result = SocialSecurityCalculator.CalcMedical(settings);
        // 20000 * 0.02 = 400
        Assert.Equal(400m, result);
    }

    [Fact]
    public void CalcMedical_Tier2_UsesTier2Rate()
    {
        var settings = new AppSettings
        {
            GrossSalary = 20000,
            MedicalTier = MedicalTier.Tier2,
            MedicalInputMode = InsuranceInputMode.Auto,
            SocialSecurity = new SocialSecurityParams()
        };
        var result = SocialSecurityCalculator.CalcMedical(settings);
        // 20000 * 0.005 = 100
        Assert.Equal(100m, result);
    }

    [Fact]
    public void CalcHousingFund_BaseRate_MultipliesBaseAndRate()
    {
        var settings = new AppSettings
        {
            HousingFundMode = HousingFundMode.BaseRate,
            HousingFundBase = 12000,
            HousingFundRate = 0.08m
        };
        var result = SocialSecurityCalculator.CalcHousingFund(settings);
        // 12000 * 0.08 = 960
        Assert.Equal(960m, result);
    }

    [Fact]
    public void CalcHousingFund_BaseRateCap_ClampsBaseToCap()
    {
        var settings = new AppSettings
        {
            HousingFundMode = HousingFundMode.BaseRateCap,
            HousingFundBase = 40000,
            HousingFundCap = 34860,
            HousingFundRate = 0.05m
        };
        var result = SocialSecurityCalculator.CalcHousingFund(settings);
        // min(40000, 34860) = 34860 → 34860 * 0.05 = 1743
        Assert.Equal(1743m, result);
    }

    [Fact]
    public void ComputeAll_SummarizesAllInsurance()
    {
        var settings = new AppSettings
        {
            GrossSalary = 20000,
            PensionInputMode = InsuranceInputMode.Auto,
            MedicalTier = MedicalTier.Tier1,
            MedicalInputMode = InsuranceInputMode.Auto,
            UnemploymentInputMode = InsuranceInputMode.Auto,
            HousingFundMode = HousingFundMode.BaseRate,
            HousingFundBase = 20000,
            HousingFundRate = 0.05m,
            SocialSecurity = new SocialSecurityParams()
        };

        var breakdown = SocialSecurityCalculator.ComputeAll(settings);

        Assert.Equal(1600m, breakdown.Pension);      // 20000*0.08
        Assert.Equal(400m, breakdown.Medical);       // 20000*0.02
        Assert.Equal(40m, breakdown.Unemployment);   // 20000*0.002
        Assert.Equal(1000m, breakdown.HousingFund);  // 20000*0.05
        Assert.Equal(3040m, breakdown.Total);        // 1600+400+40+1000
    }
}
