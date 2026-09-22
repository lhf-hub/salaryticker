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
        settings.CurrentMonth = 1;
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

        // 进度应该在 0-1 之间
        Assert.InRange(snapshot.MonthProgress, 0, 1);
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
}
