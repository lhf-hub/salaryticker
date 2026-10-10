// ============================================================================
// 文件名：MuyuSessionTests.cs
// 功能描述：摸鱼会话管理单元测试（手动结束 / 退出时的中断收尾 / 老数据兼容）
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Models;
using SalaryTicker.Services;
using ShenzhenSalaryTimer.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

public class MuyuSessionTests : IDisposable
{
    private readonly string _testDir;
    private readonly MuyuHistoryService _history;

    public MuyuSessionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SalaryTickerMuyu_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _history = new MuyuHistoryService(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private static DateTimeOffset CreateLocalTime(int year, int month, int day, int hour, int minute, int second = 0)
    {
        var localDateTime = new DateTime(year, month, day, hour, minute, second);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }

    private static AppSettings CreateSettings()
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
        // 用户的实际班制：8:00-17:30 上班、12:00-13:30 午休
        settings.Shift.WorkStart = new TimeSpan(8, 0, 0);
        settings.Shift.WorkEnd = new TimeSpan(17, 30, 0);
        settings.Shift.LunchBreaks = new List<LunchBreak>
        {
            new() { Start = new TimeSpan(12, 0, 0), End = new TimeSpan(13, 30, 0) }
        };
        return settings;
    }

    private MuyuSessionManager CreateManager(FakeTimeProvider timeProvider, AppSettings settings)
        => new(_history, new SalaryEngine(timeProvider), timeProvider, () => settings);

    // ========== 手动结束（保持原有行为） ==========

    [Fact]
    public void Stop_RecordsSessionWithoutInterruptedFlag()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0)); // 周三
        var manager = CreateManager(time, settings);

        var ended = 0;
        manager.SessionEnded += (_, _) => ended++;

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(5));
        var record = manager.Stop();

        Assert.NotNull(record);
        Assert.False(record!.Interrupted);
        Assert.Equal(300, record.DurationSeconds);
        Assert.True(record.CostAmount > 0);
        Assert.Null(manager.Active);
        Assert.Single(_history.Records);
        Assert.Equal(1, ended);
    }

    // ========== 退出时的中断收尾 ==========

    [Fact]
    public void StopInterrupted_RecordsInProgressSessionWithFlag()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0));
        var manager = CreateManager(time, settings);

        manager.Start("water", "☕ 倒杯水");
        time.Advance(TimeSpan.FromMinutes(3));
        var record = manager.StopInterrupted();

        Assert.NotNull(record);
        Assert.True(record!.Interrupted);
        Assert.Equal("☕ 倒杯水", record.ButtonLabel);
        Assert.Equal(180, record.DurationSeconds);
        Assert.Null(manager.Active);

        // 关键：这段计时必须落盘，而不是随退出消失
        Assert.Single(_history.Records);
        Assert.True(_history.Records[0].Interrupted);
    }

    [Fact]
    public void StopInterrupted_DoesNotRaiseSessionEnded()
    {
        // 界面正在关闭，没必要（也不该）再走一遍 UI 更新
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0));
        var manager = CreateManager(time, settings);

        var ended = 0;
        manager.SessionEnded += (_, _) => ended++;

        manager.Start("smoke", "🚬 抽根烟");
        time.Advance(TimeSpan.FromMinutes(2));
        manager.StopInterrupted();

        Assert.Equal(0, ended);
    }

    [Fact]
    public void StopInterrupted_NoActiveSession_ReturnsNullAndWritesNothing()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0));
        var manager = CreateManager(time, settings);

        Assert.Null(manager.StopInterrupted());
        Assert.Empty(_history.Records);
    }

    [Fact]
    public void StopInterrupted_ZeroDuration_IsDiscarded()
    {
        // 刚点开就退出，不该留下一条 0 秒记录
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0));
        var manager = CreateManager(time, settings);

        manager.Start("wc", "🚽 去趟 WC");
        var record = manager.StopInterrupted();

        Assert.Null(record);
        Assert.Empty(_history.Records);
        Assert.Null(manager.Active);
    }

    [Fact]
    public void StopInterrupted_Twice_SecondCallIsNoOp()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0));
        var manager = CreateManager(time, settings);

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(5));

        Assert.NotNull(manager.StopInterrupted());
        Assert.Null(manager.StopInterrupted());
        Assert.Single(_history.Records);
    }

    // ========== 「白赚」只算工作时段 ==========

    [Fact]
    public void Stop_AcrossLunch_RecordsOnlyWorkSeconds()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 11, 50, 0)); // 周三
        var manager = CreateManager(time, settings);

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(110));    // → 13:40
        var record = manager.Stop();

        Assert.NotNull(record);
        // [11:50,12:00)=600 + [13:30,13:40)=600，中间 90 分钟午休不计
        Assert.Equal(1200, record!.DurationSeconds);

        var rate = new SalaryEngine(time).ComputeSnapshot(time.GetLocalNow(), settings).SecondRate;
        Assert.Equal(1200 * rate, record.CostAmount);
    }

    [Fact]
    public void Stop_FullyInsideLunch_DiscardsButStillResetsUi()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 12, 15, 0));
        var manager = CreateManager(time, settings);

        MuyuRecord? endedWith = null;
        var endedCount = 0;
        manager.SessionEnded += (_, r) => { endedCount++; endedWith = r; };

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(45));     // → 13:00，全程午休
        var record = manager.Stop();

        Assert.Null(record);
        Assert.Empty(_history.Records);
        Assert.Null(manager.Active);

        // 关键：丢弃记录也必须通知界面复位，否则按钮会永久卡在「进行中」
        Assert.Equal(1, endedCount);
        Assert.Null(endedWith);
    }

    [Fact]
    public void Stop_SameSecond_DiscardsButStillResetsUi()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0));
        var manager = CreateManager(time, settings);

        var ended = 0;
        manager.SessionEnded += (_, _) => ended++;

        manager.Start("wc", "🚽 去趟 WC");
        var record = manager.Stop();

        Assert.Null(record);
        Assert.Empty(_history.Records);
        Assert.Equal(1, ended);
    }

    [Theory]
    [InlineData(7, 7, 0)]    // 上班前
    [InlineData(7, 19, 0)]   // 下班后
    public void Stop_OutsideWorkHours_IsDiscarded(int day, int hour, int minute)
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, day, hour, minute, 0));
        var manager = CreateManager(time, settings);

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(10));

        Assert.Null(manager.Stop());
        Assert.Empty(_history.Records);
    }

    [Fact]
    public void Stop_OnWeekend_IsDiscarded()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 10, 10, 0, 0)); // 周六
        var manager = CreateManager(time, settings);

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(5));

        Assert.Null(manager.Stop());
        Assert.Empty(_history.Records);
    }

    [Fact]
    public void Stop_OnHoliday_IsDiscarded()
    {
        var settings = CreateSettings();
        settings.HolidayDates = new List<HolidayEntry>
        {
            new() { Date = new DateOnly(2026, 1, 7), Name = "测试节假日" }
        };
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0)); // 周三，被设为节假日
        var manager = CreateManager(time, settings);

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(5));

        Assert.Null(manager.Stop());
        Assert.Empty(_history.Records);
    }

    [Fact]
    public void Stop_OnMakeupWeekend_IsRecorded()
    {
        var settings = CreateSettings();
        settings.MakeupWorkDays = new List<HolidayEntry>
        {
            new() { Date = new DateOnly(2026, 1, 4), Name = "测试补班" }
        };
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 4, 10, 0, 0)); // 周日补班
        var manager = CreateManager(time, settings);

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(5));
        var record = manager.Stop();

        Assert.NotNull(record);
        Assert.Equal(300, record!.DurationSeconds);
        Assert.True(record.CostAmount > 0);
        Assert.False(record.Interrupted);
    }

    [Fact]
    public void StopInterrupted_DuringLunch_RecordsMorningOnly()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 11, 0, 0));
        var manager = CreateManager(time, settings);

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(90));     // → 12:30，已进入午休
        var record = manager.StopInterrupted();

        Assert.NotNull(record);
        Assert.True(record!.Interrupted);
        Assert.Equal(3600, record.DurationSeconds); // 只算 [11:00,12:00)
    }

    [Fact]
    public void StopInterrupted_OutsideWorkHours_DiscardsWithoutEvent()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 12, 15, 0));
        var manager = CreateManager(time, settings);

        var ended = 0;
        manager.SessionEnded += (_, _) => ended++;

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(30));

        Assert.Null(manager.StopInterrupted());
        Assert.Empty(_history.Records);
        Assert.Equal(0, ended);   // 退出收尾不触发界面事件
    }

    [Fact]
    public void GetLiveUpdate_DuringLunch_FreezesWorkTimeButNotWallClock()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 11, 50, 0));
        var manager = CreateManager(time, settings);

        manager.Start("wc", "🚽 去趟 WC");
        time.Advance(TimeSpan.FromMinutes(20));     // → 12:10

        var at1210 = manager.GetLiveUpdate();
        Assert.NotNull(at1210);
        Assert.Equal(TimeSpan.FromMinutes(20), at1210!.Elapsed);        // 墙上继续走
        Assert.Equal(TimeSpan.FromMinutes(10), at1210.WorkElapsed);     // 只到 12:00

        time.Advance(TimeSpan.FromMinutes(30));     // → 12:40，仍在午休

        var at1240 = manager.GetLiveUpdate();
        Assert.NotNull(at1240);
        Assert.Equal(TimeSpan.FromMinutes(50), at1240!.Elapsed);
        Assert.Equal(TimeSpan.FromMinutes(10), at1240.WorkElapsed);     // 冻结
        Assert.Equal(at1210.Cost, at1240.Cost);                         // 金额冻结
    }

    [Fact]
    public void GetLiveUpdate_NoActiveSession_ReturnsNull()
    {
        var settings = CreateSettings();
        var time = new FakeTimeProvider(CreateLocalTime(2026, 1, 7, 10, 0, 0));
        var manager = CreateManager(time, settings);

        Assert.Null(manager.GetLiveUpdate());
    }

    // ========== 老数据兼容 ==========

    [Fact]
    public void HistoryFileWithoutInterruptedField_LoadsAsNotInterrupted()
    {
        // 模拟 1.1.4 之前写下的 muyu-history.json：没有 Interrupted 字段
        var dir = Path.Combine(Path.GetTempPath(), $"SalaryTickerMuyuOld_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var json =
                "{\"Records\":[{\"Date\":\"2026-01-07\",\"ButtonLabel\":\"☕ 倒杯水\"," +
                "\"StartTime\":\"2026-01-07T10:00:00\",\"EndTime\":\"2026-01-07T10:10:00\"," +
                "\"DurationSeconds\":600,\"CostAmount\":15.0}]}";
            File.WriteAllText(Path.Combine(dir, "muyu-history.json"), json, new UTF8Encoding(false));

            var service = new MuyuHistoryService(dir);

            Assert.Single(service.Records);
            Assert.False(service.Records[0].Interrupted);
            Assert.Equal(600, service.Records[0].DurationSeconds);
            Assert.Equal("☕ 倒杯水", service.Records[0].ButtonLabel);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
