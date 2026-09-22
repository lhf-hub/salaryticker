// 文件名：MuyuButtonSettings.cs
// 描述：摸鱼按钮设置

namespace SalaryTicker.Models;

/// <summary>
/// 摸鱼按钮配置
/// </summary>
public class MuyuButtonSettings
{
    /// <summary>唯一 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>按钮文本</summary>
    public string Label { get; set; } = "";
    /// <summary>图标 Emoji</summary>
    public string Emoji { get; set; } = "";
}
