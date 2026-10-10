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
    /// 用户的实际班制：8:00-17:30 上班、12:00-13:30 午休（每日工作 8 小时）
    /// </summary>
    private ShiftSettings CreateUserShift()
    {
        return new ShiftSettings
        {
            PresetName = "自定义",
            WorkStart = new TimeSpan(8, 0, 0),
            WorkEnd = new TimeSpan(17, 30, 0),
            LunchBreaks = new List<LunchBreak>
            {
                new() { Start = new TimeSpan(12, 0, 0), End = new TimeSpan(13, 30, 0) }
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

    // ========== 工作时段内的秒数（摸鱼「白赚」的口径） ==========

    /// <summary>2026 年 1 月的某个本地时刻（1/7 周三、1/4 周日、1/10 周六、1/17 周六）</summary>
    private static DateTime At(int day, int hour, int minute) => new(2026, 1, day, hour, minute, 0);

    private static readonly HashSet<DateOnly> NoHolidays = new();

    [Fact]
    public void GetWorkedSecondsBetween_WithinWorkHours_CountsFully()
    {
        var shift = CreateUserShift();

        Assert.Equal(1800, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 8, 30), At(7, 9, 0), shift, NoHolidays));
    }

    [Fact]
    public void GetWorkedSecondsBetween_ClipsAtWorkEnd()
    {
        var shift = CreateUserShift();

        // 17:30 下班，之后不算
        Assert.Equal(1800, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 17, 0), At(7, 18, 0), shift, NoHolidays));
    }

    [Fact]
    public void GetWorkedSecondsBetween_SkipsLunchBreak()
    {
        var shift = CreateUserShift();

        // [11:00,12:00)=3600 + [13:30,14:00)=1800，午休 90 分钟不计
        Assert.Equal(5400, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 11, 0), At(7, 14, 0), shift, NoHolidays));
    }

    [Fact]
    public void GetWorkedSecondsBetween_FullyInLunch_ReturnsZero()
    {
        var shift = CreateUserShift();

        Assert.Equal(0, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 12, 15), At(7, 13, 0), shift, NoHolidays));
    }

    [Fact]
    public void GetWorkedSecondsBetween_StartsDuringLunch_CountsAfternoonOnly()
    {
        var shift = CreateUserShift();

        // 12:15 开始，13:30 之后才开始算
        Assert.Equal(1800, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 12, 15), At(7, 14, 0), shift, NoHolidays));
    }

    [Fact]
    public void GetWorkedSecondsBetween_FullWorkday_ReturnsTotalWorkSeconds()
    {
        var shift = CreateUserShift();

        var worked = WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 8, 0), At(7, 17, 30), shift, NoHolidays);

        Assert.Equal(28800, worked);                                         // 9.5 小时 − 1.5 小时午休
        Assert.Equal(WorkTimeCalculator.GetTotalWorkSeconds(shift), worked);
    }

    [Theory]
    [InlineData(7, 0, 0, 7, 30)]    // 上班前
    [InlineData(7, 19, 0, 20, 0)]   // 下班后
    public void GetWorkedSecondsBetween_OutsideWorkHours_ReturnsZero(int day, int fromHour, int fromMinute, int toHour, int toMinute)
    {
        var shift = CreateUserShift();

        var worked = WorkTimeCalculator.GetWorkedSecondsBetween(
            At(day, fromHour, fromMinute), At(day, toHour, toMinute), shift, NoHolidays);

        Assert.Equal(0, worked);
    }

    [Fact]
    public void GetWorkedSecondsBetween_EndNotAfterStart_ReturnsZero()
    {
        var shift = CreateUserShift();

        Assert.Equal(0, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 10, 0), At(7, 10, 0), shift, NoHolidays));
        Assert.Equal(0, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 11, 0), At(7, 10, 0), shift, NoHolidays));
    }

    [Fact]
    public void GetWorkedSecondsBetween_CrossMidnight_CountsBothSides()
    {
        var shift = CreateUserShift();

        // 周三 [17:00,17:30)=1800 + 周四 [8:00,8:30)=1800
        var worked = WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 17, 0), At(8, 8, 30), shift, NoHolidays);

        Assert.Equal(3600, worked);
    }

    [Fact]
    public void GetWorkedSecondsBetween_CrossMidnight_SkipsOvernightGap()
    {
        var shift = CreateUserShift();

        Assert.Equal(0, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 23, 0), At(8, 0, 30), shift, NoHolidays));
    }

    [Fact]
    public void GetWorkedSecondsBetween_CrossMultipleDays_CountsEachWorkSegment()
    {
        var shift = CreateUserShift();

        // 周三 [16:00,17:30)=5400 + 周四 [8:00,9:45)=6300
        var worked = WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 16, 0), At(8, 9, 45), shift, NoHolidays);

        Assert.Equal(11700, worked);
    }

    [Fact]
    public void GetWorkedSecondsBetween_Weekend_ReturnsZero()
    {
        var shift = CreateUserShift();

        Assert.Equal(0, WorkTimeCalculator.GetWorkedSecondsBetween(At(10, 10, 0), At(10, 11, 0), shift, NoHolidays));
    }

    [Fact]
    public void GetWorkedSecondsBetween_MakeupWorkday_Counts()
    {
        var shift = CreateUserShift();
        var makeup = new HashSet<DateOnly> { new(2026, 1, 4) }; // 周日补班

        var worked = WorkTimeCalculator.GetWorkedSecondsBetween(At(4, 10, 0), At(4, 11, 0), shift, NoHolidays, makeup);

        Assert.Equal(3600, worked);
    }

    [Fact]
    public void GetWorkedSecondsBetween_Holiday_ReturnsZero()
    {
        var shift = CreateUserShift();
        var holidays = new HashSet<DateOnly> { new(2026, 1, 7) }; // 周三设为节假日

        Assert.Equal(0, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 10, 0), At(7, 11, 0), shift, holidays));
    }

    [Fact]
    public void GetWorkedSecondsBetween_LunchAfterWorkEnd_DoesNotExtendPastWorkEnd()
    {
        // 设置不合理时（午休排在下班之后）不能切出一段跨越下班的工作区间：
        // GetWorkIntervals 的上一段必须截断到下班时间，否则下班后的时间会被算成工作时段
        var shift = CreateUserShift();
        shift.WorkEnd = new TimeSpan(10, 0, 0);   // 8:00-10:00，午休仍是 12:00-13:30

        // 下班后的一段时间不应算数
        Assert.Equal(0, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 10, 55), At(7, 11, 5), shift, NoHolidays));

        // 工作区间被正确截断为 [8:00, 10:00]
        Assert.Equal(7200, WorkTimeCalculator.GetWorkedSecondsBetween(At(7, 8, 0), At(7, 12, 0), shift, NoHolidays));

        var info = WorkTimeCalculator.GetDayInfo(
            new DateTimeOffset(new DateTime(2026, 1, 7, 9, 0, 0)), shift, NoHolidays);
        Assert.Single(info.Intervals);
        Assert.Equal(new TimeSpan(10, 0, 0), info.Intervals[0].End);
    }

    [Fact]
    public void GetWorkedSecondsBetween_AlternateWeek_RespectsSmallAndBigWeek()
    {
        var shift = CreateUserShift();
        shift.AlternateWeekEnabled = true;
        shift.AnchorWeekMonday = new DateOnly(2026, 1, 5);
        shift.AnchorWeekIsBig = true;
        shift.AltWorkDays = new List<DayOfWeek>
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
        };

        // 大周周六（1/10）不上班
        Assert.Equal(0, WorkTimeCalculator.GetWorkedSecondsBetween(At(10, 10, 0), At(10, 11, 0), shift, NoHolidays));

        // 小周周六（1/17）上班
        Assert.Equal(3600, WorkTimeCalculator.GetWorkedSecondsBetween(At(17, 10, 0), At(17, 11, 0), shift, NoHolidays));
    }
}
