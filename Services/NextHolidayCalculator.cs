// ============================================================================
// 文件名：NextHolidayCalculator.cs
// 功能描述：根据已配置的节假日，生成「离下一个假期还有多少天」的显示文案。
//           纯函数，便于单元测试；不依赖系统时间或全局服务。
// ============================================================================

namespace SalaryTicker.Services;

using System;
using System.Linq;
using SalaryTicker.Models;

/// <summary>
/// 下一个假期倒计时文案
/// </summary>
public static class NextHolidayCalculator
{
    /// <summary>
    /// 生成倒计时文案。
    /// - 今天在假期中：正在{名称}假期
    /// - 有未来假期：离{名称}还有 N 天
    /// - 当前年假期表已用完，且包含元旦：推断下一年元旦
    /// - 其他：假期表待更新
    /// </summary>
    public static string Describe(DateOnly today, IReadOnlyList<HolidayEntry> holidays)
    {
        if (holidays.Count == 0)
            return "假期表待更新";

        var todayHoliday = holidays.FirstOrDefault(h => h.Date == today);
        if (todayHoliday != null)
            return $"正在{todayHoliday.Name}假期";

        var next = holidays
            .Where(h => h.Date > today)
            .MinBy(h => h.Date);
        if (next != null)
        {
            int days = next.Date.DayNumber - today.DayNumber;
            return $"离{next.Name}还有 {days} 天";
        }

        int lastYear = holidays.Max(h => h.Date.Year);
        bool hasNewYear = holidays.Any(h => h.Name.Contains("元旦", StringComparison.Ordinal));
        bool hasFullYear = holidays
            .Where(h => h.Date.Year == today.Year)
            .Select(h => h.Name)
            .Distinct()
            .Count() > 1;

        // 年末空档：假期表只到当前年时，下一次固定假期就是下一年元旦。
        // 如果当前年只有元旦一条（表可能不完整），不猜测。
        if (today.Year == lastYear && hasNewYear && hasFullYear)
        {
            var nextNewYear = new DateOnly(today.Year + 1, 1, 1);
            int days = nextNewYear.DayNumber - today.DayNumber;
            return $"离元旦还有 {days} 天";
        }

        return "假期表待更新";
    }
}
