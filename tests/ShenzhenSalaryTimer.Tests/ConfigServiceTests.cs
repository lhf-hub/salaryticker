// ============================================================================
// 文件名：ConfigServiceTests.cs
// 功能描述：ConfigService 单元测试，覆盖配置持久化和异常恢复场景
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Services;
using SalaryTicker.Models;
using System;
using System.IO;
using System.Linq;
using Xunit;

/// <summary>
/// ConfigService 测试类
/// </summary>
public class ConfigServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly ConfigService _service;

    /// <summary>
    /// 测试初始化：创建临时测试目录和服务实例
    /// </summary>
    public ConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SalaryTickerTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new ConfigService(_testDir);
    }

    /// <summary>
    /// 测试清理：删除临时测试目录
    /// </summary>
    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    /// <summary>
    /// 测试首次加载时创建默认配置
    /// </summary>
    [Fact]
    public void FirstLoad_CreatesDefault()
    {
        Assert.Equal(LoadStatus.CreatedDefault, _service.LastLoadStatus);
        Assert.NotNull(_service.Settings);
        Assert.NotNull(_service.Settings.SocialSecurity);
        Assert.NotNull(_service.Settings.Tax);
        Assert.True(_service.Settings.Tax.Brackets.Count > 0);
    }

    /// <summary>
    /// 测试保存和加载的往返一致性
    /// </summary>
    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        _service.Settings.GrossSalary = 25000m;
        _service.Settings.Mode = SalaryMode.Gross;
        _service.SaveSettings();

        var service2 = new ConfigService(_testDir);
        Assert.Equal(LoadStatus.Ok, service2.LastLoadStatus);
        Assert.Equal(25000m, service2.Settings.GrossSalary);
        Assert.Equal(SalaryMode.Gross, service2.Settings.Mode);
    }

    /// <summary>
    /// 测试文件损坏时从备份恢复
    /// </summary>
    [Fact]
    public void CorruptFile_RestoresFromBackup()
    {
        // 先保存一次正常数据
        _service.Settings.GrossSalary = 30000m;
        _service.SaveSettings();

        // 手动损坏文件
        var settingsPath = Path.Combine(_testDir, "settings.json");
        File.WriteAllText(settingsPath, "{ invalid json }}}");

        // 重新加载应恢复备份
        var service2 = new ConfigService(_testDir);
        Assert.Equal(LoadStatus.RestoredFromBackup, service2.LastLoadStatus);
        Assert.Equal(30000m, service2.Settings.GrossSalary);
        Assert.Contains("已从备份", service2.LastLoadMessage);
    }

    /// <summary>
    /// 测试所有备份都损坏时重置为默认值
    /// </summary>
    [Fact]
    public void AllBackupsCorrupt_ResetsToDefault()
    {
        // 保存一次
        _service.Settings.GrossSalary = 35000m;
        _service.SaveSettings();

        // 损坏主文件和备份
        var settingsPath = Path.Combine(_testDir, "settings.json");
        File.WriteAllText(settingsPath, "{ corrupt }}}");

        var backupDir = Path.Combine(_testDir, "backup");
        if (Directory.Exists(backupDir))
        {
            foreach (var file in Directory.GetFiles(backupDir, "settings_*.json"))
                File.WriteAllText(file, "{ corrupt backup }}}");
        }

        // 应重置为默认
        var service2 = new ConfigService(_testDir);
        Assert.Equal(LoadStatus.CorruptResetToDefault, service2.LastLoadStatus);
        Assert.Contains("已重置为默认值", service2.LastLoadMessage);
    }

    /// <summary>
    /// 老用户配置里被误标为「中秋补班」的 2026-09-20，加载时应自动修正为「国庆补班」
    /// （用户配置优先于默认值，改默认值无法影响老用户，必须就地修正）
    /// </summary>
    [Fact]
    public void Load_FixesMislabeledNationalDayMakeupDay()
    {
        // 模拟老版本留下的配置：把 9/20 那条改回早期的错误名称
        var target = new DateOnly(2026, 9, 20);
        var entry = _service.Settings.MakeupWorkDays.First(e => e.Date == target);
        entry.Name = "中秋补班";
        _service.SaveSettings();

        var reloaded = new ConfigService(_testDir);

        var fixedEntry = reloaded.Settings.MakeupWorkDays.First(e => e.Date == target);
        Assert.Equal("国庆补班", fixedEntry.Name);
    }

    /// <summary>其它补班日与自定义名称不受修正影响</summary>
    [Fact]
    public void Load_LeavesOtherMakeupDaysUntouched()
    {
        _service.Settings.MakeupWorkDays.Clear();
        _service.Settings.MakeupWorkDays.Add(new HolidayEntry { Date = new DateOnly(2026, 10, 10), Name = "国庆补班" });
        _service.Settings.MakeupWorkDays.Add(new HolidayEntry { Date = new DateOnly(2026, 5, 9), Name = "劳动节补班" });
        _service.Settings.MakeupWorkDays.Add(new HolidayEntry { Date = new DateOnly(2026, 9, 20), Name = "我自己的备注" });
        _service.SaveSettings();

        var reloaded = new ConfigService(_testDir);

        Assert.Equal(3, reloaded.Settings.MakeupWorkDays.Count);
        Assert.Equal("国庆补班", reloaded.Settings.MakeupWorkDays[0].Name);
        Assert.Equal("劳动节补班", reloaded.Settings.MakeupWorkDays[1].Name);
        // 用户自定义的名称不应被"修正"覆盖
        Assert.Equal("我自己的备注", reloaded.Settings.MakeupWorkDays[2].Name);
    }

    /// <summary>
    /// 测试中文字符在 JSON 中不被转义
    /// </summary>
    [Fact]
    public void ChineseCharacters_PreservedInJson()
    {
        _service.Settings.Shift.PresetName = "五天八小时双休";
        _service.SaveSettings();

        var settingsPath = Path.Combine(_testDir, "settings.json");
        var json = File.ReadAllText(settingsPath);
        Assert.Contains("五天八小时双休", json); // 不应被转义为 \uXXXX
    }
}
