// ============================================================================
// 文件名：SalaryEngineTests.cs
// 功能描述：工资引擎单元测试
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Models;
using SalaryTicker.Services;
using ShenzhenSalaryTimer.Tests.Fakes;
using System;
using System.Collections.Generic;
using Xunit;

public class SalaryEngineTests
{
    /// <summary>
    /// 辅助方法：创建本地时间的 DateTimeOffset
    /// </summary>
    private static DateTimeOffset CreateLocalTime(int year, int month, int day, int hour, int minute, int second = 0)
    {
        var localDateTime = new DateTime(year, month, day, hour, minute, second);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }

    private AppSettings CreateTestSettings()
    {
        var settings = SettingsDefaults.Create();
        settings.Mode = SalaryMode.Gross;
        settings.GrossSalary = 20000;
        settings.PensionInputMode = InsuranceInputMode.Auto;
        settings.MedicalTier = MedicalTier.Tier1;
        settings.MedicalInputMode = InsuranceInputMode.Auto;
        settings.UnemploymentInputMode = InsuranceInputMode.Auto;
        settings.HousingFundMode = HousingFundMode.BaseRate;
        settings.HousingFundBase = 20000;
        settings.HousingFundRate = 0.05m;
        settings.SpecialDeduction = 0;
        settings.HolidayDates = new List<HolidayEntry>();
        settings.Shift.WorkStart = new TimeSpan(9, 0, 0);
        settings.Shift.WorkEnd = new TimeSpan(18, 0, 0);
        settings.Shift.LunchBreaks = new List<LunchBreak>
        {
            new() { Start = new TimeSpan(12, 0, 0), End = new TimeSpan(13, 0, 0) }
        };
        return settings;
    }

    [Fact]
    public void ComputeSnapshot_DuringWork_CalculatesCorrectly()
    {
        var settings = CreateTestSettings();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0)); // 周三 10:00
        var engine = new SalaryEngine(timeProvider);

        var snapshot = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        Assert.Equal(WorkStatus.Earning, snapshot.Status);
        Assert.True(snapshot.TodayEarned > 0);
        Assert.True(snapshot.SecondRate > 0);
        Assert.True(snapshot.DailyRate > 0);
    }

    [Fact]
    public void ComputeSnapshot_BeforeWork_ReturnsZeroTodayEarned()
    {
        var settings = CreateTestSettings();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 8, 0, 0)); // 周三 8:00
        var engine = new SalaryEngine(timeProvider);

        var snapshot = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        Assert.Equal(WorkStatus.NotStarted, snapshot.Status);
        Assert.Equal(0, snapshot.TodayEarned);
    }

    [Fact]
    public void ComputeSnapshot_Weekend_ReturnsForcedRest()
    {
        var settings = CreateTestSettings();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 10, 10, 0, 0)); // 周六 10:00
        var engine = new SalaryEngine(timeProvider);

        var snapshot = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        Assert.Equal(WorkStatus.ForcedRest, snapshot.Status);
        Assert.Equal(0, snapshot.TodayEarned);
    }

    [Fact]
    public void ComputeSnapshot_Holiday_ReturnsForcedRest()
    {
        var settings = CreateTestSettings();
        settings.HolidayDates = new List<HolidayEntry>
        {
            new() { Date = new DateOnly(2026, 1, 7), Name = "测试节假日" }
        };
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0)); // 周三（节假日）10:00
        var engine = new SalaryEngine(timeProvider);

        var snapshot = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        Assert.Equal(WorkStatus.ForcedRest, snapshot.Status);
        Assert.Equal(0, snapshot.TodayEarned);
    }

    [Fact]
    public void ComputeSnapshot_MidMonth_IncludesBackfill()
    {
        var settings = CreateTestSettings();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 15, 10, 0, 0)); // 1月15日 10:00
        var engine = new SalaryEngine(timeProvider);

        var snapshot = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        // 应该包含1月1日到1月14日的补发
        Assert.True(snapshot.MonthEarned > snapshot.TodayEarned);
    }

    [Fact]
    public void ComputeSnapshot_CrossesMidnight_RefreshesBackfillDays()
    {
        // 应用一直开着跨天时，补发天数必须跟着刷新
        // 缓存键曾经漏掉"今天"，导致跨天后本月已赚冻结在首算那天的值
        var settings = CreateTestSettings();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 15, 10, 0, 0)); // 周四 10:00
        var engine = new SalaryEngine(timeProvider);

        var day1 = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        // 同一个引擎实例、同一个 settings 引用，只把时间推进到第二天同一时刻
        timeProvider.Set(CreateLocalTime(2026, 1, 16, 10, 0, 0)); // 周五 10:00
        var day2 = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        // 今日已赚不变（同样是 9:00→10:00 一小时）
        Assert.Equal(day1.TodayEarned, day2.TodayEarned);
        Assert.Equal(day1.DailyRate, day2.DailyRate);

        // 但补发天数要多算 1/15 这一天，即整月多一个日薪
        Assert.True(Math.Abs((day1.MonthEarned + day1.DailyRate) - day2.MonthEarned) < 0.0001m,
            $"跨天后本月已赚应多一个日薪：前一天 {day1.MonthEarned}，后一天 {day2.MonthEarned}，日薪 {day1.DailyRate}");
        Assert.True(day2.MonthProgress > day1.MonthProgress);
    }

    [Fact]
    public void ComputeSnapshot_LastPayableDayOfMonth_EqualsNetMonthly()
    {
        // 10 月是节假日大月（2026 年只有 18 个计薪日）。日薪按当月摊平后，
        // 最后一个计薪日下班时「本月已赚」必须精确等于税后月薪
        // —— 这正是"节假日多的月份月底短缺"的修复目标
        var settings = CreateTestSettings();
        settings.HolidayDates = SettingsDefaults.Holidays2026();
        settings.MakeupWorkDays = SettingsDefaults.MakeupWorkDays2026();

        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 10, 30, 19, 0, 0)); // 周五 19:00（10/31 是周六）
        var engine = new SalaryEngine(timeProvider);

        var snapshot = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        Assert.Equal(WorkStatus.OffWork, snapshot.Status);
        Assert.Equal(snapshot.NetMonthly / 18m, snapshot.DailyRate);
        Assert.True(Math.Abs(snapshot.MonthEarned - snapshot.NetMonthly) < 0.01m,
            $"月底本月已赚应等于税后月薪：已赚 {snapshot.MonthEarned}，月薪 {snapshot.NetMonthly}");
        Assert.True(snapshot.MonthProgress >= 0.9999,
            $"月底进度应到 100%：{snapshot.MonthProgress}");
    }

    [Fact]
    public void ComputeSnapshot_MonthChanges_RecomputesDailyRate()
    {
        // 同一个引擎跨月时，日薪的除数（当月计薪天数）必须跟着换
        // —— 缓存键不能只认 settings 引用
        var settings = CreateTestSettings();
        settings.HolidayDates = SettingsDefaults.Holidays2026();
        settings.MakeupWorkDays = SettingsDefaults.MakeupWorkDays2026();

        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 9, 30, 10, 0, 0));
        var engine = new SalaryEngine(timeProvider);

        var sep = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        timeProvider.Set(CreateLocalTime(2026, 10, 12, 10, 0, 0));
        var oct = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        // 9 月 22 个计薪日、10 月 18 个，同一个引擎也要重算
        Assert.Equal(sep.NetMonthly / 22m, sep.DailyRate);
        Assert.Equal(oct.NetMonthly / 18m, oct.DailyRate);
        Assert.NotEqual(sep.DailyRate, oct.DailyRate);
    }

    [Fact]
    public void InvalidateCache_ClearsCachedSalary()
    {
        var settings = CreateTestSettings();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0));
        var engine = new SalaryEngine(timeProvider);

        // 第一次计算
        var snapshot1 = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        // 修改工资
        settings.GrossSalary = 30000;
        engine.InvalidateCache();

        // 第二次计算应该使用新工资
        var snapshot2 = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        Assert.True(snapshot2.DailyRate > snapshot1.DailyRate);
    }

    [Fact]
    public void ComputeSnapshot_HourEarned_UpdatesEverySecond()
    {
        var settings = CreateTestSettings();

        // 10:30:45（本整点 10:00 起在工作区间 9:00-12:00 内）
        var t1 = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 30, 45));
        var s1 = new SalaryEngine(t1).ComputeSnapshot(t1.GetLocalNow(), settings);

        // 10:30:46 下一秒
        var t2 = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 30, 46));
        var s2 = new SalaryEngine(t2).ComputeSnapshot(t2.GetLocalNow(), settings);

        // 10:00 → 10:30:45 共 1845 秒
        Assert.Equal(1845 * s1.SecondRate, s1.HourEarned);

        // 相邻两秒的增量应等于一秒薪（秒级更新，而非分钟级）
        var delta = s2.HourEarned - s1.HourEarned;
        Assert.True(Math.Abs(delta - s1.SecondRate) < 0.000001m,
            $"每秒增量应≈秒薪：增量 {delta}，秒薪 {s1.SecondRate}");

        // 且金额确实发生了变化（用户可感知的实时更新）
        Assert.NotEqual(s1.HourEarned, s2.HourEarned);
    }

    [Fact]
    public void ComputeSnapshot_HourEarned_DuringLunch_IsZero()
    {
        var settings = CreateTestSettings();
        var t = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 12, 30, 0)); // 午休中

        var snapshot = new SalaryEngine(t).ComputeSnapshot(t.GetLocalNow(), settings);

        Assert.Equal(0m, snapshot.HourEarned);
    }

    [Fact]
    public void ComputeSnapshot_HourEarned_BeforeWork_IsZero()
    {
        var settings = CreateTestSettings();
        var t = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 8, 30, 0)); // 上班前

        var snapshot = new SalaryEngine(t).ComputeSnapshot(t.GetLocalNow(), settings);

        Assert.Equal(0m, snapshot.HourEarned);
    }

    [Fact]
    public void ComputeSnapshot_MonthProgress_IsClamped()
    {
        var settings = CreateTestSettings();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 31, 19, 0, 0)); // 1月31日 19:00（下班后）
        var engine = new SalaryEngine(timeProvider);

        var snapshot = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        // 1/31 是周六，此时本月的计薪日已经全部过完，进度必须到 100%
        Assert.InRange(snapshot.MonthProgress, 0, 1);
        Assert.True(snapshot.MonthProgress >= 0.9999, $"月底进度应到 100%：{snapshot.MonthProgress}");
    }

    [Fact]
    public void ComputeSnapshot_Countdown_BeforeWork_ReturnsTimeToStart()
    {
        var settings = CreateTestSettings();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 8, 30, 0)); // 周三 8:30
        var engine = new SalaryEngine(timeProvider);

        var snapshot = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        Assert.NotNull(snapshot.Countdown);
        Assert.Equal(new TimeSpan(0, 30, 0), snapshot.Countdown.Value); // 距离9:00还有30分钟
    }

    [Fact]
    public void ComputeSnapshot_Countdown_DuringWork_ReturnsTimeToEnd()
    {
        var settings = CreateTestSettings();
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 17, 0, 0)); // 周三 17:00
        var engine = new SalaryEngine(timeProvider);

        var snapshot = engine.ComputeSnapshot(timeProvider.GetLocalNow(), settings);

        Assert.NotNull(snapshot.Countdown);
        Assert.Equal(new TimeSpan(1, 0, 0), snapshot.Countdown.Value); // 距离18:00还有1小时
    }

    // ========== 本月剩余工作日 ==========

    // 测试用的班制是周一至周五 8:00-17:30；节假日被清空、补班日保留默认的 2026 年表，
    // 其中 1/4（周日）是元旦补班。所以 2026 年 1 月的计薪天数 = 22 个工作日 + 1 天补班。
    private const int Jan2026PayableDays = 23;

    private static HashSet<DateOnly> DatesOf(List<HolidayEntry> entries)
    {
        var set = new HashSet<DateOnly>();
        foreach (var e in entries) set.Add(e.Date);
        return set;
    }

    [Fact]
    public void ComputeSnapshot_RemainingWorkDays_ExcludesToday()
    {
        var settings = CreateTestSettings();
        var t = new FakeTimeProvider(CreateLocalTime(2026, 1, 15, 10, 0, 0)); // 周四

        // 1/1、1/2、1/4（补班）、1/5~1/9、1/12~1/14 —— 已过 11 个计薪日
        Assert.Equal(Jan2026PayableDays, WorkTimeCalculator.GetPayableDaysInMonth(
            2026, 1, settings.Shift, DatesOf(settings.HolidayDates), DatesOf(settings.MakeupWorkDays)));

        var snapshot = new SalaryEngine(t).ComputeSnapshot(t.GetLocalNow(), settings);

        // 23 - 11 已过 - 1 今天 = 11（今天算"正在上的这一天"，不算"还剩的一天"）
        Assert.Equal(11, snapshot.RemainingWorkDays);
    }

    [Fact]
    public void ComputeSnapshot_RemainingWorkDays_SameThroughoutTheDay()
    {
        // 上班前、午休中、下班后取到的剩余工作日必须一致 ——
        // 否则会在上班期间"不知不觉少一天"
        var settings = CreateTestSettings();
        var expected = -1;

        foreach (var hour in new[] { 8, 12, 19 })
        {
            var t = new FakeTimeProvider(CreateLocalTime(2026, 1, 15, hour, 30, 0));
            var snapshot = new SalaryEngine(t).ComputeSnapshot(t.GetLocalNow(), settings);

            if (expected < 0) expected = snapshot.RemainingWorkDays;
            Assert.Equal(expected, snapshot.RemainingWorkDays);
        }

        Assert.Equal(11, expected);
    }

    [Fact]
    public void ComputeSnapshot_RemainingWorkDays_DecrementsNextDay()
    {
        var settings = CreateTestSettings();

        var wed = new FakeTimeProvider(CreateLocalTime(2026, 1, 14, 10, 0, 0)); // 周三
        var thu = new FakeTimeProvider(CreateLocalTime(2026, 1, 15, 10, 0, 0)); // 周四

        var before = new SalaryEngine(wed).ComputeSnapshot(wed.GetLocalNow(), settings);
        var after = new SalaryEngine(thu).ComputeSnapshot(thu.GetLocalNow(), settings);

        Assert.Equal(before.RemainingWorkDays - 1, after.RemainingWorkDays);
    }

    [Fact]
    public void ComputeSnapshot_RemainingWorkDays_OnWeekend_DoesNotSubtractToday()
    {
        var settings = CreateTestSettings();
        var t = new FakeTimeProvider(CreateLocalTime(2026, 1, 10, 10, 0, 0)); // 周六，不计薪

        var snapshot = new SalaryEngine(t).ComputeSnapshot(t.GetLocalNow(), settings);

        // 已过 8 个（1/1、1/2、1/4 补班、1/5~1/9）；今天不计薪，所以不减那一个 1
        Assert.Equal(Jan2026PayableDays - 8, snapshot.RemainingWorkDays);
    }

    [Fact]
    public void ComputeSnapshot_RemainingWorkDays_MakeupDayCountsAsWorkday()
    {
        var settings = CreateTestSettings();
        var t = new FakeTimeProvider(CreateLocalTime(2026, 1, 4, 10, 0, 0)); // 周日，但是元旦补班

        var snapshot = new SalaryEngine(t).ComputeSnapshot(t.GetLocalNow(), settings);

        Assert.Equal(WorkStatus.Earning, snapshot.Status);
        // 已过 2 个（1/1、1/2）；今天是补班日，按计薪日算，减那一个 1
        Assert.Equal(Jan2026PayableDays - 3, snapshot.RemainingWorkDays);
    }

    [Fact]
    public void ComputeSnapshot_RemainingWorkDays_ZeroOnLastPayableDay()
    {
        var settings = CreateTestSettings();
        var t = new FakeTimeProvider(CreateLocalTime(2026, 1, 30, 19, 0, 0)); // 周五，本月最后一个计薪日

        var snapshot = new SalaryEngine(t).ComputeSnapshot(t.GetLocalNow(), settings);

        Assert.Equal(0, snapshot.RemainingWorkDays);
    }

    [Fact]
    public void ComputeSnapshot_RemainingWorkDays_StaysZeroAfterMonthEnd()
    {
        var settings = CreateTestSettings();
        var t = new FakeTimeProvider(CreateLocalTime(2026, 1, 31, 10, 0, 0)); // 周六，本月已走完

        var snapshot = new SalaryEngine(t).ComputeSnapshot(t.GetLocalNow(), settings);

        Assert.Equal(0, snapshot.RemainingWorkDays);
    }
}
