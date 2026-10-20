// ============================================================================
// 文件名：SplashWindow.xaml.cs
// 功能描述：启动温馨提示页代码后置（自适应屏幕尺寸）
// ============================================================================

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
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

        Loaded += (_, _) =>
        {
            FitToScreen();
            StartButton.Focus();
        };

        ViewModel = (SplashViewModel)DataContext;
    }

    /// <summary>
    /// 自适应屏幕：窗口不超出工作区
    /// </summary>
    private void FitToScreen()
    {
        WindowFit.Fit(this);
    }

    /// <summary>
    /// 打开默认邮件客户端
    /// </summary>
    private void Email_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
