// ============================================================================
// 文件名：CountdownFormatterTests.cs
// 功能描述：倒计时显示格式化的单元测试（主界面与迷你窗共用）
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Models;
using SalaryTicker.Services;
using System;
using Xunit;

public class CountdownFormatterTests
{
    // ========== 时间格式 ==========

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(3, "00:03")]
    [InlineData(5 * 60 + 3, "05:03")]
    [InlineData(59 * 60 + 59, "59:59")]          // 差一秒满一小时，仍不带小时位
    [InlineData(60 * 60, "01:00:00")]            // 满一小时开始带小时位
    [InlineData(2 * 3600 + 15 * 60 + 30, "02:15:30")]
    [InlineData(12 * 3600, "12:00:00")]
    [InlineData(26 * 3600 + 30 * 60, "26:30:00")] // 小时位不封顶
    public void Format_RendersHoursOnlyWhenNeeded(int seconds, string expected)
    {
        Assert.Equal(expected, CountdownFormatter.Format(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Format_Negative_ClampsToZero()
    {
        Assert.Equal("00:00", CountdownFormatter.Format(TimeSpan.FromSeconds(-90)));
    }

    [Fact]
    public void Format_IgnoresSubSecondRemainder()
    {
        Assert.Equal("02:15", CountdownFormatter.Format(TimeSpan.FromSeconds(135.8)));
    }

    // ========== 短标签文案 ==========

    [Theory]
    [InlineData(WorkStatus.Earning, 2 * 3600 + 15 * 60 + 30, "距下班 02:15:30")]
    [InlineData(WorkStatus.LunchBreak, 30 * 60, "距下班 30:00")]
    [InlineData(WorkStatus.NotStarted, 45 * 60, "距上班 45:00")]
    public void GetShortText_WithCountdown_PrefixesLabel(WorkStatus status, int seconds, string expected)
    {
        Assert.Equal(expected, CountdownFormatter.GetShortText(TimeSpan.FromSeconds(seconds), status));
    }

    [Theory]
    [InlineData(WorkStatus.OffWork)]
    [InlineData(WorkStatus.ForcedRest)]
    public void GetShortText_WithoutCountdown_ReturnsEmpty(WorkStatus status)
    {
        Assert.Equal("", CountdownFormatter.GetShortText(null, status));
    }

    [Theory]
    [InlineData(WorkStatus.OffWork)]
    [InlineData(WorkStatus.ForcedRest)]
    public void GetShortText_WithoutLabel_ReturnsEmptyEvenWhenValuePresent(WorkStatus status)
    {
        Assert.Equal("", CountdownFormatter.GetShortText(TimeSpan.FromHours(1), status));
    }
}
