// ============================================================================
// 文件名：WorkTimeCalculator.cs
// 功能描述：班制计算器（工作日判定、大小周锚点、时段状态机、补发天数）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 班制计算器
/// </summary>
public static class WorkTimeCalculator
{
    /// <summary>
    /// 判断指定日期是否为计薪日
    /// 优先级：补班日（周末也上班计薪） &gt; 节假日（强制休息） &gt; 常规工作日
    /// </summary>
    public static bool IsPayableDay(DateOnly date, ShiftSettings shift, ISet<DateOnly> holidays, ISet<DateOnly>? makeupWorkDays = null)
    {
        // 1. 国家规定的补班日：即使是周末也正常上班计薪
        if (makeupWorkDays != null && makeupWorkDays.Contains(date))
            return true;

        // 2. 节假日强制休息
        if (holidays.Contains(date))
            return false;

        // 3. 常规工作日判定（含大小周）
        var workDays = GetWorkDaysForWeek(date, shift);
        return workDays.Contains(date.DayOfWeek);
    }

    /// <summary>
    /// 获取指定日期所在周的工作日列表（考虑大小周）
    /// </summary>
    public static List<DayOfWeek> GetWorkDaysForWeek(DateOnly date, ShiftSettings shift)
    {
        if (!shift.AlternateWeekEnabled)
            return shift.WorkDays;

        // 大小周逻辑
        var dateDT = date.ToDateTime(TimeOnly.MinValue);
        var anchorDT = shift.AnchorWeekMonday.ToDateTime(TimeOnly.MinValue);

        // 计算该日期的周一
        var offset = ((int)dateDT.DayOfWeek + 6) % 7;
        var monday = dateDT.AddDays(-offset);

        // 计算与锚点周的周差
        var weeksDiff = (int)Math.Floor((monday - anchorDT).TotalDays / 7.0);
        var isBigWeek = (weeksDiff % 2 == 0) == shift.AnchorWeekIsBig;

        return isBigWeek ? shift.WorkDays : shift.AltWorkDays;
    }

    /// <summary>
    /// 获取指定日期的工作状态和已工作秒数
    /// </summary>
    public static DayWorkInfo GetDayInfo(DateTimeOffset now, ShiftSettings shift, ISet<DateOnly> holidays, ISet<DateOnly>? makeupWorkDays = null)
    {
        // 统一使用本地时间
        var localNow = now.LocalDateTime;
        var today = DateOnly.FromDateTime(localNow);
        var currentTime = localNow.TimeOfDay;

        // 非计薪日（节假日/周末，且非补班日）
        if (!IsPayableDay(today, shift, holidays, makeupWorkDays))
        {
            return new DayWorkInfo(WorkStatus.ForcedRest, 0, 0, GetTotalWorkSeconds(shift), null, new List<WorkInterval>());
        }

        // 获取工作区间
        var intervals = GetWorkIntervals(shift);

        // 判断状态和计算已工作秒数
        var (status, workedSeconds) = CalculateWorkStatus(currentTime, intervals, shift);

        // 计算倒计时
        TimeSpan? countdown = CalculateCountdown(currentTime, shift, status);

        return new DayWorkInfo(status, workedSeconds, 0, GetTotalWorkSeconds(shift), countdown, intervals);
    }

    /// <summary>
    /// 统计 [start, end) 内落在工作时段里的秒数（跨天、跨午休均支持）
    /// 与「今日已赚」同口径：只认计薪日（补班日算、节假日与周末不算），
    /// 且只算每日工作区间内的时间（已扣除午休）
    /// </summary>
    public static long GetWorkedSecondsBetween(
        DateTime start, DateTime end, ShiftSettings shift,
        ISet<DateOnly> holidays, ISet<DateOnly>? makeupWorkDays = null)
    {
        if (end <= start) return 0;

        // 工作区间只由班制决定（按周而不是按日），循环外算一次
        var intervals = GetWorkIntervals(shift);
        var totalSeconds = 0L;

        for (var date = DateOnly.FromDateTime(start); date <= DateOnly.FromDateTime(end); date = date.AddDays(1))
        {
            if (!IsPayableDay(date, shift, holidays, makeupWorkDays))
                continue;

            // 会话与该自然日的交集
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            var segStart = start > dayStart ? start : dayStart;
            var segEnd = end < dayEnd ? end : dayEnd;
            if (segEnd <= segStart) continue;

            // 转成"当日 0 点起的偏移"以便与工作区间比较。
            // TimeSpan 没有 24:00，交集右端落在次日 0 点时取整日长度。
            var rangeStart = segStart.TimeOfDay;
            var rangeEnd = segStart.Date == segEnd.Date ? segEnd.TimeOfDay : TimeSpan.FromDays(1);

            foreach (var interval in intervals)
            {
                var overlapStart = rangeStart > interval.Start ? rangeStart : interval.Start;
                var overlapEnd = rangeEnd < interval.End ? rangeEnd : interval.End;

                if (overlapEnd > overlapStart)
                    totalSeconds += (long)(overlapEnd - overlapStart).TotalSeconds;
            }
        }

        return totalSeconds;
    }

    /// <summary>
    /// 获取当月补发天数（从1号到昨天）
    /// </summary>
    public static int GetMonthBackfillDays(int year, int month, DateOnly today, ShiftSettings shift, ISet<DateOnly> holidays, ISet<DateOnly>? makeupWorkDays = null)
    {
        var days = 0;
        var startDate = new DateOnly(year, month, 1);
        var endDate = today.AddDays(-1);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (IsPayableDay(date, shift, holidays, makeupWorkDays))
                days++;
        }

        return days;
    }

    /// <summary>
    /// 获取指定月份的总计薪天数（整月）
    /// 用于"按当月摊平"的日薪：日薪 = 税后月薪 ÷ 当月计薪天数，月底累计正好等于月薪
    /// </summary>
    public static int GetPayableDaysInMonth(int year, int month, ShiftSettings shift, ISet<DateOnly> holidays, ISet<DateOnly>? makeupWorkDays = null)
    {
        var days = 0;
        var daysInMonth = DateTime.DaysInMonth(year, month);

        for (var day = 1; day <= daysInMonth; day++)
        {
            if (IsPayableDay(new DateOnly(year, month, day), shift, holidays, makeupWorkDays))
                days++;
        }

        return days;
    }

    /// <summary>
    /// 获取每日总工作秒数
    /// </summary>
    public static long GetTotalWorkSeconds(ShiftSettings shift)
    {
        var totalSeconds = (shift.WorkEnd - shift.WorkStart).TotalSeconds;
        foreach (var lunch in shift.LunchBreaks)
        {
            totalSeconds -= (lunch.End - lunch.Start).TotalSeconds;
        }
        return (long)totalSeconds;
    }

    /// <summary>
    /// 获取工作区间列表（排除午休）
    /// </summary>
    private static List<WorkInterval> GetWorkIntervals(ShiftSettings shift)
    {
        var intervals = new List<WorkInterval>();
        var workStart = shift.WorkStart;
        var workEnd = shift.WorkEnd;

        // 排序午休时段
        var lunches = shift.LunchBreaks.OrderBy(l => l.Start).ToList();

        var current = workStart;
        foreach (var lunch in lunches)
        {
            // 上段必须截断到下班时间：若午休开始得比下班还晚（设置不合理时可能出现），
            // 直接 add(current, lunch.Start) 会切出一段跨越下班的工作区间
            if (lunch.Start > current)
            {
                var segmentEnd = lunch.Start < workEnd ? lunch.Start : workEnd;
                if (segmentEnd > current)
                    intervals.Add(new WorkInterval(current, segmentEnd));
            }
            current = lunch.End > current ? lunch.End : current;
        }

        if (workEnd > current)
        {
            intervals.Add(new WorkInterval(current, workEnd));
        }

        return intervals;
    }

    /// <summary>
    /// 计算工作状态和已工作秒数
    /// </summary>
    private static (WorkStatus status, long workedSeconds) CalculateWorkStatus(
        TimeSpan currentTime, List<WorkInterval> intervals, ShiftSettings shift)
    {
        // 上班前
        if (currentTime < shift.WorkStart)
            return (WorkStatus.NotStarted, 0);

        // 下班后
        if (currentTime >= shift.WorkEnd)
            return (WorkStatus.OffWork, GetTotalWorkSeconds(shift));

        // 检查是否在午休
        foreach (var lunch in shift.LunchBreaks)
        {
            if (currentTime >= lunch.Start && currentTime < lunch.End)
            {
                // 午休中，计算上午已工作秒数
                var morningWorked = intervals
                    .Where(i => i.End <= lunch.Start)
                    .Sum(i => (long)(i.End - i.Start).TotalSeconds);
                return (WorkStatus.LunchBreak, morningWorked);
            }
        }

        // 工作中，计算已工作秒数
        var workedSeconds = 0L;
        foreach (var interval in intervals)
        {
            if (currentTime <= interval.Start)
                break;

            if (currentTime >= interval.End)
            {
                workedSeconds += (long)(interval.End - interval.Start).TotalSeconds;
            }
            else
            {
                workedSeconds += (long)(currentTime - interval.Start).TotalSeconds;
            }
        }

        return (WorkStatus.Earning, workedSeconds);
    }

    /// <summary>
    /// 计算倒计时
    /// </summary>
    private static TimeSpan? CalculateCountdown(TimeSpan currentTime, ShiftSettings shift, WorkStatus status)
    {
        return status switch
        {
            WorkStatus.NotStarted => shift.WorkStart - currentTime,
            WorkStatus.Earning or WorkStatus.LunchBreak => shift.WorkEnd - currentTime,
            _ => null
        };
    }
}

/// <summary>
/// 工作区间
/// </summary>
public record WorkInterval(TimeSpan Start, TimeSpan End);

/// <summary>
/// 日工作信息
/// </summary>
public record DayWorkInfo(
    WorkStatus Status,
    long WorkedSeconds,
    long HourWorkedSeconds,
    long TotalDaySeconds,
    TimeSpan? Countdown,
    IReadOnlyList<WorkInterval> Intervals);
