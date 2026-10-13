// ============================================================================
// 文件名：NumericInputTests.cs
// 功能描述：数值输入过滤的单元测试
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Services;
using Xunit;

public class NumericInputTests
{
    [Theory]
    [InlineData("", "123")]
    [InlineData("12", "3")]
    [InlineData("", "10050")]
    [InlineData("", "0")]
    public void IsAcceptable_Digits_Allowed(string current, string incoming)
    {
        Assert.True(NumericInput.IsAcceptable(current, incoming));
    }

    [Theory]
    [InlineData("", "abc")]
    [InlineData("", "12abc")]      // 粘贴一整段
    [InlineData("12", "a")]
    [InlineData("", "-5")]         // 负号也不放行
    [InlineData("", "１２３")]      // 全角数字：绑定转换不认，必须拒掉
    public void IsAcceptable_NonNumeric_Rejected(string current, string incoming)
    {
        Assert.False(NumericInput.IsAcceptable(current, incoming));
    }

    [Fact]
    public void IsAcceptable_FirstDecimalPoint_Allowed()
    {
        Assert.True(NumericInput.IsAcceptable("", "."));
        Assert.True(NumericInput.IsAcceptable("12", "."));
        Assert.True(NumericInput.IsAcceptable("", "0.5"));
    }

    [Theory]
    [InlineData("1.5", ".")]       // 已经有小数点了
    [InlineData("1.", "5.")]
    [InlineData("", "1.2.3")]      // 一段里两个点
    public void IsAcceptable_SecondDecimalPoint_Rejected(string current, string incoming)
    {
        Assert.False(NumericInput.IsAcceptable(current, incoming));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("12", null)]
    [InlineData(null, "")]
    public void IsAcceptable_EmptyInput_Allowed(string? current, string? incoming)
    {
        Assert.True(NumericInput.IsAcceptable(current, incoming));
    }
}
