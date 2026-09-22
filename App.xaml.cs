// ============================================================================
// 文件名：App.xaml.cs
// 功能描述：应用入口与组合根（单实例保护、服务装配、窗口流编排）
// ============================================================================

using System.Threading;
using System.Windows;
using SalaryTicker.Services;
using SalaryTicker.Views;

namespace SalaryTicker;

/// <summary>
/// 应用入口
/// </summary>
public partial class App : Application
{
    /// <summary>单实例互斥体：防止双开导致并发写坏 JSON 配置文件</summary>
    private Mutex? _instanceMutex;

    /// <summary>本进程是否真正持有了互斥体（只有持有者才能 ReleaseMutex）</summary>
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例保护
        _instanceMutex = new Mutex(true, "SalaryTicker_SingleInstance", out bool createdNew);
        _ownsMutex = createdNew;

        if (!createdNew)
        {
            // 未抢到互斥体：本进程并不持有它，不能调用 ReleaseMutex（否则抛 ApplicationException）
            _instanceMutex.Dispose();
            _instanceMutex = null;

            MessageBox.Show("SalaryTicker 实时计算器已在运行中，请勿重复打开。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // 装配服务
        AppServices.Initialize();

        // 命令行参数 gallery：打开样式画廊窗口（设计系统验收用）
        if (e.Args.Contains("gallery"))
        {
            var gallery = new StyleGalleryWindow();
            gallery.Closed += (_, _) => Shutdown();
            gallery.Show();
            return;
        }

        // 判断是否需要显示启动页
        var showSplash = AppServices.Config.Settings.ShowSplashOnStartup;

        if (showSplash)
        {
            var splash = new SplashWindow();
            splash.ViewModel.Confirmed += (_, args) =>
            {
                // 勾选"下次不再显示"则持久化
                if (args.DontShowAgain)
                {
                    AppServices.Config.Settings.ShowSplashOnStartup = false;
                    AppServices.Config.SaveSettings();
                }

                // 淡出后打开主窗口
                FadeOutAndOpenMain(splash);
            };
            splash.Show();
        }
        else
        {
            OpenMainWindow();
        }
    }

    /// <summary>
    /// 淡出启动页并打开主窗口
    /// </summary>
    private void FadeOutAndOpenMain(Window splash)
    {
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = splash.Opacity,
            To = 0,
            Duration = new Duration(System.TimeSpan.FromSeconds(0.2))
        };

        fadeOut.Completed += (_, _) =>
        {
            splash.Close();
            OpenMainWindow();
        };

        splash.BeginAnimation(Window.OpacityProperty, fadeOut);
    }

    /// <summary>
    /// 打开主窗口
    /// </summary>
    private void OpenMainWindow()
    {
        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 统一收敛退出路径：停止计时器 + 刷新运行时状态
        try
        {
            AppServices.Shutdown();
        }
        catch
        {
            // 退出流程中的异常不应阻塞应用关闭
        }

        // 只有真正持有互斥体时才释放，且失败不应阻塞退出
        try
        {
            if (_ownsMutex && _instanceMutex != null)
                _instanceMutex.ReleaseMutex();
        }
        catch
        {
            // 忽略：退出流程中的释放异常不应导致崩溃
        }

        _instanceMutex?.Dispose();
        _instanceMutex = null;
        base.OnExit(e);
    }
}
