// ============================================================================
// 文件名：CountdownFormatter.cs
// 功能描述：倒计时显示的格式化纯逻辑（主界面与迷你窗共用，便于单元测试）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;

/// <summary>
/// 倒计时显示格式化
/// </summary>
public static class CountdownFormatter
{
    /// <summary>
    /// 格式化为 时:分:秒；不足 1 小时时省略小时位，显示 分:秒
    /// </summary>
    public static string Format(TimeSpan countdown)
    {
        // 防御：按班制算出的倒计时不会为负，但界面不该出现负号
        if (countdown < TimeSpan.Zero) countdown = TimeSpan.Zero;

        return countdown.TotalHours >= 1
            ? $"{(int)countdown.TotalHours:D2}:{countdown.Minutes:D2}:{countdown.Seconds:D2}"
            : $"{countdown.Minutes:D2}:{countdown.Seconds:D2}";
    }

    /// <summary>
    /// 短标签文案，如「距下班 02:15:30」
    /// 下班后与强制休息没有倒计时，返回空串
    /// </summary>
    public static string GetShortText(TimeSpan? countdown, WorkStatus status)
    {
        if (countdown == null) return "";

        var label = status switch
        {
            WorkStatus.NotStarted => "距上班",
            WorkStatus.Earning or WorkStatus.LunchBreak => "距下班",
            _ => ""
        };

        return label.Length == 0 ? "" : $"{label} {Format(countdown.Value)}";
    }
}
