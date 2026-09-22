// ============================================================================
// 文件名：SplashWindow.xaml.cs
// 功能描述：启动温馨提示页代码后置（自适应屏幕尺寸）
// ============================================================================

using System;
using System.Windows;
using SalaryTicker.ViewModels;

namespace SalaryTicker.Views;

/// <summary>
/// 启动页窗口
/// </summary>
public partial class SplashWindow : Window
{
    /// <summary>启动页 ViewModel（供 App 订阅确认事件）</summary>
    public SplashViewModel ViewModel { get; }

    public SplashWindow()
    {
        InitializeComponent();

        FitToScreen();

        ViewModel = (SplashViewModel)DataContext;
    }

    /// <summary>
    /// 自适应屏幕：窗口不超出工作区
    /// </summary>
    private void FitToScreen()
    {
        var work = SystemParameters.WorkArea;

        Width = Math.Min(Width, work.Width - 40);
        Height = Math.Min(Height, work.Height - 40);

        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;
    }
}
