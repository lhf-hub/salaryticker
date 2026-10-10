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
        settings.Shift.WorkStart = new TimeSpan(9, 0, 0);
        settings.Shift.WorkEnd = new TimeSpan(18, 0, 0);
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
