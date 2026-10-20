// ============================================================================
// 文件名：NextHolidayCalculatorTests.cs
// 功能描述：下一个假期倒计时文案的单元测试。
// ============================================================================

using System;
using SalaryTicker.Models;
using SalaryTicker.Services;
using Xunit;

namespace ShenzhenSalaryTimer.Tests;

public class NextHolidayCalculatorTests
{
    private static HolidayEntry H(string date, string name) =>
        new() { Date = DateOnly.Parse(date), Name = name };

    [Fact]
    public void ReturnsNextHolidayCountdown()
    {
        var holidays = new[] { H("2026-10-01", "国庆"), H("2027-01-01", "元旦") };

        Assert.Equal(
            "离元旦还有 73 天",
            NextHolidayCalculator.Describe(new DateOnly(2026, 10, 20), holidays));
    }

    [Fact]
    public void ReturnsInHolidayMessage()
    {
        var holidays = new[]
        {
            H("2026-10-01", "国庆"),
            H("2026-10-02", "国庆"),
            H("2026-10-03", "国庆")
        };

        Assert.Equal(
            "正在国庆假期",
            NextHolidayCalculator.Describe(new DateOnly(2026, 10, 2), holidays));
    }

    [Fact]
    public void FallsBackToNextNewYearWhenCurrentYearHolidaysAreExhausted()
    {
        var holidays = new[] { H("2026-01-01", "元旦"), H("2026-10-01", "国庆") };

        Assert.Equal(
            "离元旦还有 73 天",
            NextHolidayCalculator.Describe(new DateOnly(2026, 10, 20), holidays));
    }

    [Fact]
    public void ReturnsUpdateHintWhenHolidayTableIsOutdated()
    {
        var holidays = new[] { H("2026-01-01", "元旦"), H("2026-10-01", "国庆") };

        Assert.Equal(
            "假期表待更新",
            NextHolidayCalculator.Describe(new DateOnly(2027, 1, 2), holidays));
    }

    [Fact]
    public void ReturnsUpdateHintWhenTableIsEmpty()
    {
        Assert.Equal(
            "假期表待更新",
            NextHolidayCalculator.Describe(new DateOnly(2026, 10, 20), Array.Empty<HolidayEntry>()));
    }
}
