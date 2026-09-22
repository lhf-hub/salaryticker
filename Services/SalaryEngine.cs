// ============================================================================
// 文件名：SalaryEngine.cs
// 功能描述：工资引擎（组合SalaryCalculator + WorkTimeCalculator，含缓存）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// 工资引擎
/// </summary>
public sealed class SalaryEngine
{
    private readonly TimeProvider _timeProvider;
    private SalaryResult? _cachedSalary;
    private AppSettings? _cachedSettings;
    private (int year, int month)? _cachedBackfill;
    private int _cachedBackfillDays;

    public SalaryEngine(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 计算工资快照
    /// </summary>
    public SalarySnapshot ComputeSnapshot(DateTimeOffset now, AppSettings settings)
    {
        // 计算税后工资（带缓存）
        var salary = GetSalary(settings);

        // 获取节假日集合
        var holidays = new HashSet<DateOnly>();
        foreach (var h in settings.HolidayDates)
            holidays.Add(h.Date);

        // 获取补班日集合（国家规定的调休补班日，周末也上班计薪）
        var makeupWorkDays = new HashSet<DateOnly>();
        foreach (var m in settings.MakeupWorkDays)
            makeupWorkDays.Add(m.Date);

        // 获取今日工作信息
        var dayInfo = WorkTimeCalculator.GetDayInfo(now, settings.Shift, holidays, makeupWorkDays);

        // 计算当月补发
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        var backfillDays = GetBackfillDays(today.Year, today.Month, today, settings.Shift, holidays, settings, makeupWorkDays);

        // 计算各项收入
        var todayEarned = dayInfo.WorkedSeconds * salary.SecondRate;
        var monthEarned = (backfillDays * salary.DailyRate) + todayEarned;

        // 计算当前小时已赚：本整点至今与今天工作区间的重叠秒数 × 秒薪（秒级精确）
        var hourEarned = ComputeHourEarned(now, dayInfo.Intervals, salary.SecondRate);

        // 计算本月进度
        var totalMonthDays = GetTotalPayableDaysInMonth(today.Year, today.Month, settings.Shift, holidays, makeupWorkDays);
        var monthProgress = totalMonthDays > 0 ? (double)(monthEarned / (totalMonthDays * salary.DailyRate)) : 0;
        monthProgress = Math.Clamp(monthProgress, 0, 1);

        return new SalarySnapshot
        {
            Now = now,
            Status = dayInfo.Status,
            IsMuyu = false, // 由 MuyuSessionManager 管理
            MonthEarned = monthEarned,
            TodayEarned = todayEarned,
            HourEarned = hourEarned,
            DailyRate = salary.DailyRate,
            SecondRate = salary.SecondRate,
            NetMonthly = salary.NetMonthly,
            Countdown = dayInfo.Countdown,
            MonthProgress = monthProgress
        };
    }

    /// <summary>
    /// 计算"当前小时已赚"：从本整点开始累计，只统计落在工作区间内的秒数
    /// </summary>
    private static decimal ComputeHourEarned(DateTimeOffset now, IReadOnlyList<WorkInterval> intervals, decimal secondRate)
    {
        var localNow = now.LocalDateTime;
        var hourStart = new TimeSpan(localNow.Hour, 0, 0);
        var currentTime = localNow.TimeOfDay;

        long seconds = 0;
        foreach (var interval in intervals)
        {
            // 求 [hourStart, currentTime] 与工作区间的重叠
            var overlapStart = hourStart > interval.Start ? hourStart : interval.Start;
            var overlapEnd = currentTime < interval.End ? currentTime : interval.End;

            if (overlapEnd > overlapStart)
                seconds += (long)(overlapEnd - overlapStart).TotalSeconds;
        }

        return seconds * secondRate;
    }

    /// <summary>
    /// 使缓存失效
    /// </summary>
    public void InvalidateCache()
    {
        _cachedSalary = null;
        _cachedSettings = null;
        _cachedBackfill = null;
    }

    /// <summary>
    /// 获取税后工资（带缓存）
    /// </summary>
    private SalaryResult GetSalary(AppSettings settings)
    {
        if (_cachedSettings != settings || _cachedSalary == null)
        {
            _cachedSalary = SalaryCalculator.Compute(settings);
            _cachedSettings = settings;
        }
        return _cachedSalary;
    }

    /// <summary>
    /// 获取补发天数（带缓存）
    /// </summary>
    private int GetBackfillDays(int year, int month, DateOnly today, ShiftSettings shift, ISet<DateOnly> holidays, AppSettings settings, ISet<DateOnly> makeupWorkDays)
    {
        if (_cachedBackfill == (year, month) && _cachedSettings == settings)
            return _cachedBackfillDays;

        _cachedBackfillDays = WorkTimeCalculator.GetMonthBackfillDays(year, month, today, shift, holidays, makeupWorkDays);
        _cachedBackfill = (year, month);
        return _cachedBackfillDays;
    }

    /// <summary>
    /// 获取当月总计薪天数
    /// </summary>
    private int GetTotalPayableDaysInMonth(int year, int month, ShiftSettings shift, ISet<DateOnly> holidays, ISet<DateOnly> makeupWorkDays)
    {
        var days = 0;
        var daysInMonth = DateTime.DaysInMonth(year, month);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            if (WorkTimeCalculator.IsPayableDay(date, shift, holidays, makeupWorkDays))
                days++;
        }

        return days;
    }
}
