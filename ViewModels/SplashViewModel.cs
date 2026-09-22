// ============================================================================
// 文件名：SplashViewModel.cs
// 功能描述：启动页 ViewModel
// ============================================================================

namespace SalaryTicker.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

/// <summary>
/// 启动页 ViewModel
/// </summary>
public partial class SplashViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _dontShowAgain;

    /// <summary>
    /// 确认命令（触发关闭启动页）
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        // 后续阶段：在此读取 Settings.ShowSplashOnStartup，如果 DontShowAgain=true 则写入 false
        // 触发窗口关闭（通过事件或回调）
        Confirmed?.Invoke(this, new SplashConfirmedEventArgs(DontShowAgain));
    }

    /// <summary>
    /// 确认事件
    /// </summary>
    public event EventHandler<SplashConfirmedEventArgs>? Confirmed;
}

/// <summary>
/// 启动页确认事件参数
/// </summary>
public class SplashConfirmedEventArgs : EventArgs
{
    public bool DontShowAgain { get; }

    public SplashConfirmedEventArgs(bool dontShowAgain)
    {
        DontShowAgain = dontShowAgain;
    }
}
