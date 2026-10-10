// ============================================================================
// 文件名：WorkTimeCalculatorTests.cs
// 功能描述：班制计算器单元测试
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Models;
using SalaryTicker.Services;
using ShenzhenSalaryTimer.Tests.Fakes;
using System;
using System.Collections.Generic;
using Xunit;

public class WorkTimeCalculatorTests
{
    private ShiftSettings CreateDefaultShift()
    {
        return new ShiftSettings
        {
            PresetName = "五天八小时双休",
            WorkStart = new TimeSpan(9, 0, 0),
            WorkEnd = new TimeSpan(18, 0, 0),
            LunchBreaks = new List<LunchBreak>
            {
                new() { Start = new TimeSpan(12, 0, 0), End = new TimeSpan(13, 0, 0) }
            },
            WorkDays = new List<DayOfWeek>
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday
            },
            AlternateWeekEnabled = false
        };
    }

    /// <summary>
    /// 辅助方法：创建本地时间的 DateTimeOffset
    /// </summary>
    private static DateTimeOffset CreateLocalTime(int year, int month, int day, int hour, int minute, int second = 0)
    {
        var localDateTime = new DateTime(year, month, day, hour, minute, second);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }

    [Fact]
    public void IsPayableDay_Workday_ReturnsTrue()
    {
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var wednesday = new DateOnly(2026, 1, 7); // 周三

        Assert.True(WorkTimeCalculator.IsPayableDay(wednesday, shift, holidays));
    }

    [Fact]
    public void IsPayableDay_Weekend_ReturnsFalse()
    {
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var saturday = new DateOnly(2026, 1, 10); // 周六

        Assert.False(WorkTimeCalculator.IsPayableDay(saturday, shift, holidays));
    }

    [Fact]
    public void IsPayableDay_Holiday_ReturnsFalse()
    {
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly> { new DateOnly(2026, 1, 7) }; // 周三设为节假日
        var wednesday = new DateOnly(2026, 1, 7);

        Assert.False(WorkTimeCalculator.IsPayableDay(wednesday, shift, holidays));
    }

    [Fact]
    public void IsPayableDay_MakeupWorkDay_WeekendBecomesPayable()
    {
        // 周日补班：国家规定的调休补班日，即使是周末也应正常计薪
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var makeup = new HashSet<DateOnly> { new DateOnly(2026, 1, 4) }; // 2026-01-04 是周日
        var sunday = new DateOnly(2026, 1, 4);

        Assert.True(WorkTimeCalculator.IsPayableDay(sunday, shift, holidays, makeup));
    }

    [Fact]
    public void IsPayableDay_WeekendWithoutMakeup_StillForcedRest()
    {
        // 未登记的周末仍是强制休息（双休）
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var makeup = new HashSet<DateOnly> { new DateOnly(2026, 1, 4) };
        var otherSaturday = new DateOnly(2026, 1, 10);

        Assert.False(WorkTimeCalculator.IsPayableDay(otherSaturday, shift, holidays, makeup));
    }

    [Fact]
    public void GetDayInfo_OnMakeupDay_ReturnsEarningNotForcedRest()
    {
        // 补班日（周日）上班时间应正常计时
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var makeup = new HashSet<DateOnly> { new DateOnly(2026, 1, 4) };
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 4, 10, 0, 0)); // 周日 10:00

        var info = WorkTimeCalculator.GetDayInfo(timeProvider.GetLocalNow(), shift, holidays, makeup);

        Assert.Equal(WorkStatus.Earning, info.Status);
        Assert.Equal(3600, info.WorkedSeconds);
    }

    [Fact]
    public void GetTotalWorkSeconds_Standard_ReturnsCorrect()
    {
        var shift = CreateDefaultShift();
        // 9:00-18:00 = 9小时，午休1小时，实际8小时 = 28800秒
        Assert.Equal(28800, WorkTimeCalculator.GetTotalWorkSeconds(shift));
    }

    [Fact]
    public void GetDayInfo_BeforeWork_ReturnsNotStarted()
    {
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 8, 0, 0)); // 周三 8:00

        var info = WorkTimeCalculator.GetDayInfo(timeProvider.GetLocalNow(), shift, holidays);

        Assert.Equal(WorkStatus.NotStarted, info.Status);
        Assert.Equal(0, info.WorkedSeconds);
        Assert.NotNull(info.Countdown);
        Assert.Equal(new TimeSpan(1, 0, 0), info.Countdown.Value); // 距离9:00还有1小时
    }

    [Fact]
    public void GetDayInfo_DuringWork_ReturnsEarning()
    {
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0)); // 周三 10:00

        var info = WorkTimeCalculator.GetDayInfo(timeProvider.GetLocalNow(), shift, holidays);

        Assert.Equal(WorkStatus.Earning, info.Status);
        Assert.Equal(3600, info.WorkedSeconds); // 9:00-10:00 = 1小时
    }

    [Fact]
    public void GetDayInfo_DuringLunch_ReturnsLunchBreak()
    {
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 12, 30, 0)); // 周三 12:30

        var info = WorkTimeCalculator.GetDayInfo(timeProvider.GetLocalNow(), shift, holidays);

        Assert.Equal(WorkStatus.LunchBreak, info.Status);
        Assert.Equal(10800, info.WorkedSeconds); // 9:00-12:00 = 3小时
    }

    [Fact]
    public void GetDayInfo_AfterWork_ReturnsOffWork()
    {
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 19, 0, 0)); // 周三 19:00

        var info = WorkTimeCalculator.GetDayInfo(timeProvider.GetLocalNow(), shift, holidays);

        Assert.Equal(WorkStatus.OffWork, info.Status);
        Assert.Equal(28800, info.WorkedSeconds); // 完整工作日
    }

    [Fact]
    public void GetMonthBackfillDays_NoHolidays_ReturnsWorkdays()
    {
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly>();
        var today = new DateOnly(2026, 1, 15); // 周四

        // 1月1日到1月14日的工作日（排除周末）
        // 2026年1月：1日是周四
        // 工作日：1,2,5,6,7,8,9,12,13,14 = 10天
        var days = WorkTimeCalculator.GetMonthBackfillDays(2026, 1, today, shift, holidays);

        Assert.Equal(10, days);
    }

    [Fact]
    public void GetMonthBackfillDays_WithHolidays_ExcludesHolidays()
    {
        var shift = CreateDefaultShift();
        var holidays = new HashSet<DateOnly> { new DateOnly(2026, 1, 5) }; // 周一设为节假日
        var today = new DateOnly(2026, 1, 8); // 周四

        // 1月1日到1月7日，排除5日（节假日）
        // 工作日：1,2,6,7 = 4天
        var days = WorkTimeCalculator.GetMonthBackfillDays(2026, 1, today, shift, holidays);

        Assert.Equal(4, days);
    }

    [Theory]
    [InlineData(2026, 1, 21)]   // 元旦假期，1/4 补班
    [InlineData(2026, 9, 22)]   // 中秋，9/20 国庆补班
    [InlineData(2026, 10, 18)]  // 国庆长假，10/10 补班
    public void GetPayableDaysInMonth_Uses2026HolidayAndMakeupTables(int year, int month, int expected)
    {
        var shift = CreateDefaultShift();

        var holidays = new HashSet<DateOnly>();
        foreach (var h in SettingsDefaults.Holidays2026())
            holidays.Add(h.Date);

        var makeupWorkDays = new HashSet<DateOnly>();
        foreach (var m in SettingsDefaults.MakeupWorkDays2026())
            makeupWorkDays.Add(m.Date);

        var days = WorkTimeCalculator.GetPayableDaysInMonth(year, month, shift, holidays, makeupWorkDays);

        Assert.Equal(expected, days);
    }

    [Fact]
    public void GetWorkDaysForWeek_AlternateWeek_ReturnsCorrect()
    {
        var shift = new ShiftSettings
        {
            WorkDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            AltWorkDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday },
            AlternateWeekEnabled = true,
            AnchorWeekMonday = new DateOnly(2026, 1, 5), // 周一
            AnchorWeekIsBig = true
        };

        // 锚点周（大周）：1月5日所在周
        var bigWeekSaturday = new DateOnly(2026, 1, 10); // 周六
        Assert.DoesNotContain(DayOfWeek.Saturday, WorkTimeCalculator.GetWorkDaysForWeek(bigWeekSaturday, shift));

        // 下一周（小周）：1月12日所在周
        var smallWeekSaturday = new DateOnly(2026, 1, 17); // 周六
        Assert.Contains(DayOfWeek.Saturday, WorkTimeCalculator.GetWorkDaysForWeek(smallWeekSaturday, shift));
    }
}
