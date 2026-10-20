// ============================================================================
// 文件名：App.xaml.cs
// 功能描述：应用入口与组合根（单实例保护、服务装配、窗口流编排）
// ============================================================================

using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SalaryTicker.Dialogs;
using SalaryTicker.Services;
using SalaryTicker.ViewModels;
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
        // smoke：构造并测量全部窗口，用于捕获 build/test 看不见的 XAML 运行时错误。
        // 它不显示窗口、不启动计时器，也不参与单实例保护。
        bool isSmoke = e.Args.Contains("smoke");

        if (!isSmoke)
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
        }

        base.OnStartup(e);

        // 装配服务
        AppServices.Initialize();

        if (isSmoke)
        {
            RunSmokeTest();
            return;
        }

        // 开发/验收用单窗口模式：只打开指定窗口，便于截图与目检。
        if (e.Args.Contains("spike"))
        {
            ShowSingleWindow(new DesignSpikeWindow());
            return;
        }
        if (e.Args.Contains("confirm"))
        {
            ConfirmDialog.Show(null,
                "确定要清空所有摸鱼历史记录吗？此操作不可恢复。",
                "清空摸鱼历史",
                isDanger: true);
            Shutdown();
            return;
        }
        if (e.Args.Contains("gallery"))
        {
            ShowSingleWindow(new StyleGalleryWindow());
            return;
        }
        if (e.Args.Contains("warm"))
        {
            ShowSingleWindow(new WarmGalleryWindow());
            return;
        }
        if (e.Args.Contains("shot-main") || e.Args.Contains("shot-mini"))
        {
            RunScreenshot(e.Args);
            return;
        }
        if (e.Args.Contains("main"))
        {
            ShowSingleWindow(new MainWindow());
            return;
        }
        if (e.Args.Contains("history"))
        {
            var main = new MainWindow();
            main.Loaded += (_, _) => (main.DataContext as MainViewModel)?.ShowHistoryCommand.Execute(null);
            ShowSingleWindow(main);
            return;
        }
        if (e.Args.Contains("settings"))
        {
            ShowSingleWindow(new SettingsWindow());
            return;
        }
        if (e.Args.Contains("mini"))
        {
            // 截图/目检时启动 ticker 并提供满不透明度，模拟真实使用状态。
            AppServices.Ticker.Start();
            AppServices.Ticker.RefreshNow();
            ShowSingleWindow(new MiniWindow { Opacity = 1.0 });
            return;
        }
        if (e.Args.Contains("splash"))
        {
            ShowSingleWindow(new SplashWindow());
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
    /// 只显示一个窗口（开发/验收用），关闭后退出应用。
    /// </summary>
    private void ShowSingleWindow(Window window)
    {
        window.Closed += (_, _) => Shutdown();
        window.Show();
    }

    /// <summary>
    /// 开发用：把主窗 / 迷你窗以 1.25x 渲染为 PNG（不含 Windows 标题栏），供官网素材使用。
    /// 用法：SalaryTicker.exe shot-main &lt;输出路径&gt; / shot-mini &lt;输出路径&gt;
    /// </summary>
    private void RunScreenshot(string[] args)
    {
        bool isMini = args.Contains("shot-mini");
        string mode = isMini ? "shot-mini" : "shot-main";
        int index = Array.IndexOf(args, mode);
        string output = index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : Path.Combine(Path.GetTempPath(), isMini ? "SalaryTicker-mini.png" : "SalaryTicker-main.png");

        Window window;
        if (isMini)
        {
            AppServices.Ticker.Start();
            AppServices.Ticker.RefreshNow();
            window = new MiniWindow { Opacity = 1.0 };
        }
        else
        {
            window = new MainWindow();
        }

        window.ShowInTaskbar = false;
        window.Show();
        // WindowFit 会在 Loaded 中居中；这里再移到屏幕外，截图时不打扰桌面。
        window.Left = -2400;
        window.Top = 40;
        window.UpdateLayout();

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(isMini ? 700 : 1800)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!isMini)
                HideScrollBars(window);
            window.UpdateLayout();

            var root = (FrameworkElement)window.Content;
            if (!isMini && root is Panel panel && panel.Background == null)
                panel.Background = window.Background;

            // 渲染 root 的父级（客户区），这样迷你窗 6px 的外边距也会保留。
            var visual = VisualTreeHelper.GetParent(root) as FrameworkElement ?? root;

            const double scale = 1.25;
            int width = (int)Math.Round(visual.ActualWidth * scale);
            int height = (int)Math.Round(visual.ActualHeight * scale);
            var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(output))
                encoder.Save(stream);

            window.Close();
            Shutdown();
        };
        timer.Start();
    }

    private static void HideScrollBars(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
                viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            HideScrollBars(child);
        }
    }

    /// <summary>
    /// 构造并测量全部窗口。任何 XamlParseException / 缺失资源 / 无效模板都会在这里暴露。
    /// 不显示窗口，因此不会触发 Loaded（计时器/更新检查不会启动）。
    /// </summary>
    private void RunSmokeTest()
    {
        var failures = new List<string>();

        void Check(string name, Func<Window> factory)
        {
            try
            {
                var window = factory();
                window.ApplyTemplate();
                window.Measure(new Size(1200, 900));
                window.Arrange(new Rect(0, 0, 1200, 900));
                window.UpdateLayout();
                Console.WriteLine($"[smoke] {name}: OK");
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine($"[smoke] {name}: FAIL");
                Console.Error.WriteLine(ex);
            }
        }

        Check("MainWindow", () => new MainWindow());
        Check("SettingsWindow", () => new SettingsWindow());
        Check("SplashWindow", () => new SplashWindow());
        Check("MiniWindow", () => new MiniWindow());
        Check("ConfirmDialog", () => new ConfirmDialog());
        Check("StyleGalleryWindow", () => new StyleGalleryWindow());
        Check("WarmGalleryWindow", () => new WarmGalleryWindow());
        Check("DesignSpikeWindow", () => new DesignSpikeWindow());

        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"[smoke] FAILED: {failures.Count} window(s)");
            foreach (var failure in failures)
                Console.Error.WriteLine("  " + failure);
            Shutdown(1);
            return;
        }

        Console.WriteLine("[smoke] all windows OK");
        Shutdown(0);
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
