// ============================================================================
// 文件名：UpdateLogic.cs
// 功能描述：更新判定的纯逻辑（版本比较 / 检查节流 / 跳过判定）
//           全部为无副作用的静态方法，便于单元测试
// ============================================================================

namespace SalaryTicker.Services;

using System;

/// <summary>
/// 更新判定纯逻辑
/// </summary>
public static class UpdateLogic
{
    /// <summary>两次检查之间的最小间隔（避免频繁请求服务器）</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    /// <summary>
    /// 解析版本号字符串（容错处理）
    /// 支持 "1.0.1"、"1.0.1.0"、"1.0.1+build.123"；非法输入返回 null
    /// </summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 去掉 SemVer 的构建元数据后缀（如 1.0.1+abc123）
        var cleaned = text.Trim();
        var plusIndex = cleaned.IndexOf('+');
        if (plusIndex >= 0) cleaned = cleaned[..plusIndex];

        // 去掉预发布后缀（如 1.0.1-beta）
        var dashIndex = cleaned.IndexOf('-');
        if (dashIndex >= 0) cleaned = cleaned[..dashIndex];

        return Version.TryParse(cleaned, out var version) ? version : null;
    }

    /// <summary>
    /// 判断远端版本是否比当前版本更新（相等或更旧返回 false）
    /// </summary>
    public static bool IsNewer(string? currentVersion, string? remoteVersion)
    {
        var current = ParseVersion(currentVersion);
        var remote = ParseVersion(remoteVersion);

        // 任一版本号非法时，保守地判定为"没有更新"
        if (current == null || remote == null) return false;

        return remote > current;
    }

    /// <summary>
    /// 判断现在是否应该发起更新检查
    /// </summary>
    /// <param name="enabled">用户是否开启了"检查更新"</param>
    /// <param name="url">更新地址（为空表示未配置，不检查）</param>
    /// <param name="lastCheckUtc">上次检查时间（UTC）</param>
    /// <param name="nowUtc">当前时间（UTC）</param>
    public static bool ShouldCheckNow(bool enabled, string? url, DateTime? lastCheckUtc, DateTime nowUtc)
    {
        if (!enabled) return false;
        if (string.IsNullOrWhiteSpace(url)) return false;

        if (lastCheckUtc.HasValue && nowUtc - lastCheckUtc.Value < CheckInterval)
            return false;

        return true;
    }

    /// <summary>
    /// 判断是否应该向用户提示这个新版本（用户已忽略的版本不再提示）
    /// </summary>
    public static bool ShouldNotify(string? currentVersion, string? remoteVersion, string? skippedVersion)
    {
        if (!IsNewer(currentVersion, remoteVersion)) return false;

        var remote = ParseVersion(remoteVersion);
        var skipped = ParseVersion(skippedVersion);

        // 用户明确忽略过这个确切版本 → 不再打扰
        if (remote != null && skipped != null && remote == skipped) return false;

        return true;
    }

    /// <summary>
    /// 校验 SHA256 是否一致（忽略大小写与空白）
    /// </summary>
    public static bool Sha256Matches(string? actual, string? expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected)) return false;
        return string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
