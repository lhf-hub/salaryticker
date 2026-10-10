// ============================================================================
// 文件名：OffWorkReminderTests.cs
// 功能描述：下班到点提醒的单元测试（边沿判定 / 每日去重 / 响铃与停止）
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Models;
using SalaryTicker.Services;
using ShenzhenSalaryTimer.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;

public class OffWorkReminderTests
{
    private static DateTimeOffset CreateLocalTime(int year, int month, int day, int hour, int minute, int second = 0)
    {
        var localDateTime = new DateTime(year, month, day, hour, minute, second);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }

    private static AppSettings CreateSettings(bool reminderEnabled)
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
        settings.MakeupWorkDays = new List<HolidayEntry>();
        settings.Shift.WorkStart = new TimeSpan(9, 0, 0);
        settings.Shift.WorkEnd = new TimeSpan(18, 0, 0);
        settings.OffWorkReminderEnabled = reminderEnabled;
        return settings;
    }

    private static SalaryTickerService CreateTicker(TimeProvider timeProvider, AppSettings settings)
        => new(new SalaryEngine(timeProvider), timeProvider, () => settings);

    // ========== 边沿判定 ==========

    [Theory]
    [InlineData(null, WorkStatus.OffWork, false)]            // 首帧就是下班（启动时已过下班点）→ 不响
    [InlineData(WorkStatus.Earning, WorkStatus.OffWork, true)]
    [InlineData(WorkStatus.LunchBreak, WorkStatus.OffWork, true)]
    [InlineData(WorkStatus.NotStarted, WorkStatus.OffWork, true)]
    [InlineData(WorkStatus.OffWork, WorkStatus.OffWork, false)]  // 重复快照不算新边沿
    [InlineData(WorkStatus.Earning, WorkStatus.Earning, false)]
    [InlineData(WorkStatus.OffWork, WorkStatus.Earning, false)]
    public void IsOffWorkEdge_OnlyFiresOnTransitionIntoOffWork(WorkStatus? previous, WorkStatus current, bool expected)
    {
        Assert.Equal(expected, OffWorkReminderLogic.IsOffWorkEdge(previous, current));
    }

    // ========== 每日去重 ==========

    [Fact]
    public void Detector_FirstFrameOffWork_DoesNotTrigger()
    {
        var detector = new OffWorkReminderDetector();

        Assert.False(detector.Observe(new DateOnly(2026, 1, 7), WorkStatus.OffWork));
        Assert.False(detector.Observe(new DateOnly(2026, 1, 7), WorkStatus.OffWork));
    }

    [Fact]
    public void Detector_RepeatedSnapshots_TriggerOnlyOnce()
    {
        var detector = new OffWorkReminderDetector();
        var today = new DateOnly(2026, 1, 7);

        detector.Observe(today, WorkStatus.Earning);

        Assert.True(detector.Observe(today, WorkStatus.OffWork));
        // RefreshNow / 暂停恢复会重复发同一状态的快照
        Assert.False(detector.Observe(today, WorkStatus.OffWork));
        Assert.False(detector.Observe(today, WorkStatus.OffWork));
    }

    [Fact]
    public void Detector_SameDaySecondTransition_DoesNotTriggerAgain()
    {
        var detector = new OffWorkReminderDetector();
        var today = new DateOnly(2026, 1, 7);

        detector.Observe(today, WorkStatus.Earning);
        Assert.True(detector.Observe(today, WorkStatus.OffWork));

        // 改班制后再触发一次 Earning → OffWork，当天也不该再响
        detector.Observe(today, WorkStatus.Earning);
        Assert.False(detector.Observe(today, WorkStatus.OffWork));
    }

    [Fact]
    public void Detector_NextDay_TriggersAgain()
    {
        var detector = new OffWorkReminderDetector();

        detector.Observe(new DateOnly(2026, 1, 7), WorkStatus.Earning);
        Assert.True(detector.Observe(new DateOnly(2026, 1, 7), WorkStatus.OffWork));

        detector.Observe(new DateOnly(2026, 1, 8), WorkStatus.Earning);
        Assert.True(detector.Observe(new DateOnly(2026, 1, 8), WorkStatus.OffWork));
    }

    // ========== 服务层 ==========

    [Fact]
    public void Service_FirstFrameAfterWork_DoesNotRing()
    {
        var settings = CreateSettings(reminderEnabled: true);
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 19, 0, 0)); // 周三 19:00，已经下班
        var ticker = CreateTicker(timeProvider, settings);
        var plays = 0;

        using var service = new OffWorkReminderService(ticker, () => settings, () => plays++);

        ticker.RefreshNow();
        ticker.RefreshNow();

        Assert.Equal(0, plays);
        Assert.False(service.IsRinging);
    }

    [Fact]
    public void Service_Disabled_DoesNotRing()
    {
        var settings = CreateSettings(reminderEnabled: false);
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 17, 59, 0));
        var ticker = CreateTicker(timeProvider, settings);
        var plays = 0;

        using var service = new OffWorkReminderService(ticker, () => settings, () => plays++);

        ticker.RefreshNow();
        timeProvider.Set(CreateLocalTime(2026, 1, 7, 18, 0, 5));
        ticker.RefreshNow();

        Assert.Equal(0, plays);
        Assert.False(service.IsRinging);
    }

    [Fact]
    public void Service_CrossingEndTime_RingsUntilCountReached()
    {
        var settings = CreateSettings(reminderEnabled: true);
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 17, 59, 0));
        var ticker = CreateTicker(timeProvider, settings);
        var plays = 0;

        using var service = new OffWorkReminderService(
            ticker, () => settings, () => Interlocked.Increment(ref plays),
            ringInterval: TimeSpan.FromMilliseconds(20), ringCount: 3);

        ticker.RefreshNow();                                       // 17:59 还在上班
        Assert.Equal(0, Volatile.Read(ref plays));

        timeProvider.Set(CreateLocalTime(2026, 1, 7, 18, 0, 5));    // 跨过下班时间
        ticker.RefreshNow();

        Assert.True(service.IsRinging);
        Assert.Equal(1, Volatile.Read(ref plays));                 // 第一声立即响，不等一个间隔

        // 触发后重复发快照不应重启响铃
        ticker.RefreshNow();
        ticker.RefreshNow();

        WaitUntil(() => !service.IsRinging, TimeSpan.FromSeconds(5));

        Assert.False(service.IsRinging);
        Assert.Equal(3, Volatile.Read(ref plays));                 // 响满 3 次自动停
    }

    [Fact]
    public void Service_Dispose_StopsRingingImmediately()
    {
        var settings = CreateSettings(reminderEnabled: true);
        var timeProvider = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 17, 59, 0));
        var ticker = CreateTicker(timeProvider, settings);
        var plays = 0;

        var service = new OffWorkReminderService(
            ticker, () => settings, () => Interlocked.Increment(ref plays),
            ringInterval: TimeSpan.FromMilliseconds(30), ringCount: 100);

        ticker.RefreshNow();
        timeProvider.Set(CreateLocalTime(2026, 1, 7, 18, 0, 5));
        ticker.RefreshNow();

        Assert.True(service.IsRinging);

        service.Dispose();
        Assert.False(service.IsRinging);

        var playsAtDispose = Volatile.Read(ref plays);
        Thread.Sleep(150);
        Assert.Equal(playsAtDispose, Volatile.Read(ref plays));
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
            Thread.Sleep(10);
    }
}
