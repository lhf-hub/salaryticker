// ============================================================================
// 文件名：MuyuButtonViewModel.cs
// 功能描述：摸鱼按钮 ViewModel
// ============================================================================

namespace SalaryTicker.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using SalaryTicker.Models;

/// <summary>
/// 摸鱼按钮 ViewModel
/// </summary>
public partial class MuyuButtonViewModel : ObservableObject
{
    /// <summary>按钮 ID</summary>
    public string Id { get; }

    /// <summary>按钮文字</summary>
    public string Label { get; }

    /// <summary>按钮 Emoji</summary>
    public string Emoji { get; }

    /// <summary>显示文本（Emoji + 文字）</summary>
    public string DisplayText => $"{Emoji} {Label}";

    /// <summary>是否为当前活动会话</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>本次摸鱼时长文本（如 "05:23"）</summary>
    [ObservableProperty]
    private string _elapsedText = "";

    /// <summary>本次摸鱼白赚金额文本（如 "¥7.55"）</summary>
    [ObservableProperty]
    private string _costText = "";

    /// <summary>是否正在摸鱼中</summary>
    [ObservableProperty]
    private bool _isRunning;

    public MuyuButtonViewModel(MuyuButtonSettings settings)
    {
        Id = settings.Id;
        Label = settings.Label;
        Emoji = settings.Emoji;
    }
}
