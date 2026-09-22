// ============================================================================
// 文件名：UpdateManifest.cs
// 功能描述：版本清单（服务端 version.json）的数据模型
// ============================================================================

namespace SalaryTicker.Models;

using System;

/// <summary>
/// 版本清单：由服务端托管在 &lt;域名&gt;/version.json，应用启动后拉取用于比对版本
/// </summary>
public class UpdateManifest
{
    /// <summary>最新版本号（如 "1.0.1"）</summary>
    public string Version { get; set; } = "";

    /// <summary>下载地址（建议 HTTPS，指向 releases/SalaryTicker-&lt;版本&gt;.exe）</summary>
    public string Url { get; set; } = "";

    /// <summary>安装包 SHA256（大写十六进制），用于下载完整性校验</summary>
    public string Sha256 { get; set; } = "";

    /// <summary>安装包字节数（用于显示进度，可为 0）</summary>
    public long Size { get; set; }

    /// <summary>官网地址（用于"程序目录不可写时"引导用户手动下载）</summary>
    public string SiteUrl { get; set; } = "";

    /// <summary>更新说明（显示在更新提示条上）</summary>
    public string ReleaseNotes { get; set; } = "";

    /// <summary>发布时间</summary>
    public DateTimeOffset? PublishedAt { get; set; }
}
