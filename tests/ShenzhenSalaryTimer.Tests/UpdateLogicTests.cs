// ============================================================================
// 文件名：UpdateLogicTests.cs
// 功能描述：更新判定纯逻辑的单元测试（版本比较 / 节流 / 跳过 / 哈希校验）
// ============================================================================

namespace ShenzhenSalaryTimer.Tests;

using SalaryTicker.Models;
using SalaryTicker.Services;
using System;
using System.Text.Json;
using Xunit;

public class UpdateLogicTests
{
    // ========== 版本解析 ==========

    [Theory]
    [InlineData("1.0.1", "1.0.1")]
    [InlineData("1.0.1.0", "1.0.1.0")]
    [InlineData("1.0.1+build.123", "1.0.1")]
    [InlineData("1.0.1-beta", "1.0.1")]
    [InlineData("  1.0.1  ", "1.0.1")]
    public void ParseVersion_HandlesCommonFormats(string input, string expected)
    {
        var parsed = UpdateLogic.ParseVersion(input);
        Assert.NotNull(parsed);
        Assert.Equal(Version.Parse(expected), parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("v1.0.1")]
    [InlineData(null)]
    public void ParseVersion_InvalidInput_ReturnsNull(string? input)
    {
        Assert.Null(UpdateLogic.ParseVersion(input));
    }

    // ========== 版本比较 ==========

    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("1.0.0", "1.0.0.1", true)]
    [InlineData("1.0.0", "1.0.0", false)]      // 相同版本不算更新
    [InlineData("1.1.0", "1.0.0", false)]      // 远端更旧，不回退
    [InlineData("1.0.0", "abc", false)]        // 远端版本号非法 → 保守判定为无更新
    [InlineData("abc", "1.0.0", false)]        // 当前版本号非法 → 同样保守
    public void IsNewer_VariousCases(string current, string remote, bool expected)
    {
        Assert.Equal(expected, UpdateLogic.IsNewer(current, remote));
    }

    // ========== 检查节流 ==========

    [Fact]
    public void ShouldCheckNow_Disabled_ReturnsFalse()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(UpdateLogic.ShouldCheckNow(false, "https://example.com/version.json", null, now));
    }

    [Fact]
    public void ShouldCheckNow_EmptyUrl_ReturnsFalse()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(UpdateLogic.ShouldCheckNow(true, "", null, now));
        Assert.False(UpdateLogic.ShouldCheckNow(true, "   ", null, now));
        Assert.False(UpdateLogic.ShouldCheckNow(true, null, null, now));
    }

    [Fact]
    public void ShouldCheckNow_FirstRun_ReturnsTrue()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(UpdateLogic.ShouldCheckNow(true, "https://example.com/version.json", null, now));
    }

    [Fact]
    public void ShouldCheckNow_WithinMinInterval_ReturnsFalse()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var lastCheck = now.AddMinutes(-5); // 5 分钟前（< 10 分钟）
        Assert.False(UpdateLogic.ShouldCheckNow(true, "https://example.com/version.json", lastCheck, now));
    }

    [Fact]
    public void ShouldCheckNow_ExactlyAtMinInterval_ReturnsTrue()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var lastCheck = now - UpdateLogic.MinCheckInterval;
        Assert.True(UpdateLogic.ShouldCheckNow(true, "https://example.com/version.json", lastCheck, now));
    }

    [Fact]
    public void ShouldCheckNow_HalfHourAgo_ReturnsTrue()
    {
        // 回归：以前最小间隔是 6 小时，导致"进了应用也不查"，只有手动点才弹提示
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var lastCheck = now.AddMinutes(-30);
        Assert.True(UpdateLogic.ShouldCheckNow(true, "https://example.com/version.json", lastCheck, now));
    }

    [Fact]
    public void ShouldCheckNow_Yesterday_ReturnsTrue()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var lastCheck = now.AddDays(-1);
        Assert.True(UpdateLogic.ShouldCheckNow(true, "https://example.com/version.json", lastCheck, now));
    }

    // ========== 运行中的定期检查 ==========

    [Fact]
    public void ShouldCheckPeriodically_FirstEverAttempt_ReturnsTrue()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(UpdateLogic.ShouldCheckPeriodically(DateTime.MinValue, now));
    }

    [Fact]
    public void ShouldCheckPeriodically_OneHourAgo_ReturnsFalse()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(UpdateLogic.ShouldCheckPeriodically(now.AddHours(-1), now));
    }

    [Fact]
    public void ShouldCheckPeriodically_ExactlyOnePeriod_ReturnsTrue()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(UpdateLogic.ShouldCheckPeriodically(now - UpdateLogic.PeriodicCheckInterval, now));
    }

    [Fact]
    public void ShouldCheckPeriodically_OneSecondShortOfPeriod_ReturnsFalse()
    {
        // 这条每秒都会被调用一次，判错就会变成每秒发请求
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var justUnder = now - UpdateLogic.PeriodicCheckInterval + TimeSpan.FromSeconds(1);
        Assert.False(UpdateLogic.ShouldCheckPeriodically(justUnder, now));
    }

    [Fact]
    public void ShouldCheckPeriodically_Overnight_ReturnsTrue()
    {
        var now = new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc);
        Assert.True(UpdateLogic.ShouldCheckPeriodically(now.AddHours(-15), now));
    }

    // ========== 跳过版本 ==========

    [Fact]
    public void ShouldNotify_NewVersion_ReturnsTrue()
    {
        Assert.True(UpdateLogic.ShouldNotify("1.0.0", "1.0.1", ""));
    }

    [Fact]
    public void ShouldNotify_SkippedVersion_ReturnsFalse()
    {
        // 用户已忽略 1.0.1 → 不再提示该版本
        Assert.False(UpdateLogic.ShouldNotify("1.0.0", "1.0.1", "1.0.1"));
    }

    [Fact]
    public void ShouldNotify_HigherThanSkipped_ReturnsTrue()
    {
        // 忽略过 1.0.1，但出现了 1.0.2 → 仍要提示
        Assert.True(UpdateLogic.ShouldNotify("1.0.0", "1.0.2", "1.0.1"));
    }

    [Fact]
    public void ShouldNotify_SameVersion_ReturnsFalse()
    {
        Assert.False(UpdateLogic.ShouldNotify("1.0.1", "1.0.1", ""));
    }

    [Fact]
    public void ShouldNotify_InvalidSkipped_StillNotifies()
    {
        Assert.True(UpdateLogic.ShouldNotify("1.0.0", "1.0.1", "not-a-version"));
    }

    // ========== 清单反序列化（真实 JSON 格式回归） ==========

    /// <summary>
    /// 用发布脚本实际产出的 JSON 格式（小写键名 + 中文更新日志）验证解析：
    /// 这条测试守住了两个曾经的坑 —— System.Text.Json 默认区分大小写、
    /// 以及发布脚本写出的 UTF-8/BOM 编码问题。
    /// </summary>
    [Fact]
    public void ParseManifest_RealWorldJsonWithChineseNotes()
    {
        const string json = """
        {
          "version": "1.1.1",
          "url": "https://salaryticker.leelab.com.cn/download/releases/SalaryTicker-1.1.1.exe",
          "sha256": "EF52352BC3687E0B7D871684E4A6BA09E7E786A34C0F8FA9A32ADB86063C2AAF",
          "size": 71813213,
          "siteUrl": "https://salaryticker.leelab.com.cn/",
          "releaseNotes": "官网焕新：主标题「谋生有度，生活有光。」；医疗档位明确为「深圳一档/深圳二档」",
          "publishedAt": "2026-09-21T07:45:55Z"
        }
        """;

        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, UpdateService.ManifestJsonOptions);

        Assert.NotNull(manifest);
        Assert.Equal("1.1.1", manifest!.Version);                              // 小写键名 → PascalCase 属性
        Assert.Equal(71813213, manifest.Size);
        Assert.Equal("https://salaryticker.leelab.com.cn/", manifest.SiteUrl);  // "siteUrl" 小写 u
        Assert.Contains("谋生有度", manifest.ReleaseNotes);                     // 中文不被转义/损坏
        Assert.Contains("深圳一档", manifest.ReleaseNotes);
        Assert.NotNull(manifest.PublishedAt);
        Assert.Equal(2026, manifest.PublishedAt!.Value.Year);
    }

    /// <summary>清单字段缺失时不抛异常（服务端旧版本清单也要能容忍）</summary>
    [Fact]
    public void ParseManifest_MissingOptionalFields_DoesNotThrow()
    {
        const string json = """{ "version": "1.2.0", "url": "https://example.com/a.exe" }""";

        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, UpdateService.ManifestJsonOptions);

        Assert.NotNull(manifest);
        Assert.Equal("1.2.0", manifest!.Version);
        Assert.Equal("", manifest.ReleaseNotes);
        Assert.Null(manifest.PublishedAt);
    }

    // ========== 清单地址解析（留空回退官方地址） ==========

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolveUrl_EmptyOrBlank_FallsBackToOfficial(string? configured)
    {
        Assert.Equal(UpdateService.DefaultManifestUrl, UpdateService.ResolveUrl(configured));
    }

    [Fact]
    public void ResolveUrl_CustomUrl_IsUsedAsIs()
    {
        const string custom = "https://example.com/version.json";
        Assert.Equal(custom, UpdateService.ResolveUrl(custom));
        Assert.Equal(custom, UpdateService.ResolveUrl($"  {custom}  ")); // 两端空白被裁剪
    }

    [Fact]
    public void DefaultManifestUrl_IsHttps()
    {
        Assert.StartsWith("https://", UpdateService.DefaultManifestUrl);
        Assert.EndsWith("version.json", UpdateService.DefaultManifestUrl);
    }

    // ========== SHA256 比对 ==========

    [Fact]
    public void Sha256Matches_IgnoresCaseAndWhitespace()
    {
        Assert.True(UpdateLogic.Sha256Matches("ABC123", "abc123"));
        Assert.True(UpdateLogic.Sha256Matches("  ABC123  ", "abc123"));
    }

    [Fact]
    public void Sha256Matches_DifferentOrEmpty_ReturnsFalse()
    {
        Assert.False(UpdateLogic.Sha256Matches("ABC123", "ABC124"));
        Assert.False(UpdateLogic.Sha256Matches("", "ABC123"));
        Assert.False(UpdateLogic.Sha256Matches("ABC123", ""));
        Assert.False(UpdateLogic.Sha256Matches(null, null));
    }
}
