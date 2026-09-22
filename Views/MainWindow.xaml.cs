// ============================================================================
// 文件名：MainWindow.xaml.cs
// 功能描述：主窗口代码后置（视图编排 + 自适应屏幕尺寸）
// ============================================================================

using System;
using System.Windows;
using SalaryTicker.Services;
using SalaryTicker.ViewModels;

namespace SalaryTicker.Views;

/// <summary>
/// 主窗口
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        FitToScreen();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // 订阅 ViewModel 请求
        _viewModel.SettingsRequested += OnSettingsRequested;
        _viewModel.MiniWindowToggleRequested += OnMiniWindowToggleRequested;

        // 更新流程请求重启：接力脚本会在本进程退出后替换 exe 并重新启动
        _viewModel.Update.RestartRequested += OnRestartRequestedForUpdate;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>
    /// 自适应屏幕：窗口不超出工作区，尽量一次性展示全部内容（减少滚动）
    /// </summary>
    private void FitToScreen()
    {
        var work = SystemParameters.WorkArea;

        // 留出一点屏幕边距，视觉更舒服
        Width = Math.Min(Width, work.Width - 40);
        Height = Math.Min(Height, work.Height - 40);

        // 居中显示
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;
    }

    /// <summary>
    /// 窗口加载后启动计时
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Start();

        // 异步检查更新：不阻塞启动，失败静默（内部已 try/catch）
        _ = _viewModel.Update.CheckForUpdateAsync();
    }

    /// <summary>
    /// 更新流程请求重启：先优雅退出，接力脚本会完成替换并重新启动应用
    /// </summary>
    private void OnRestartRequestedForUpdate(object? sender, EventArgs e)
    {
        // 稍作延迟，确保接力脚本已成功启动并进入等待状态
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Application.Current?.Shutdown();
        };
        timer.Start();
    }

    /// <summary>
    /// 打开设置窗口
    /// </summary>
    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        var settingsWindow = new SettingsWindow(_viewModel.Update) { Owner = this };
        var saved = settingsWindow.ShowDialog();

        if (saved == true)
        {
            // 设置已保存，刷新主界面
            _viewModel.ReloadSettings();
            AppServices.Ticker.RefreshNow();
        }
    }

    /// <summary>
    /// 切换迷你窗
    /// </summary>
    private void OnMiniWindowToggleRequested(object? sender, EventArgs e)
    {
        var miniWindow = new MiniWindow();
        miniWindow.Show();
        Hide(); // 隐藏主窗口（不关闭，保持计时）
    }

    /// <summary>
    /// 主窗口关闭：保存运行时状态
    /// </summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        AppServices.Ticker.Stop();
        Application.Current?.Shutdown();
    }
}
