// ============================================================================
// 文件名：MiniWindow.xaml.cs
// 功能描述：迷你悬浮窗代码后置（拖拽、位置记忆、透明度、返回主窗）
// ============================================================================

using System;
using System.Windows;
using System.Windows.Input;
using SalaryTicker.Models;
using SalaryTicker.Services;

namespace SalaryTicker.Views;

/// <summary>
/// 迷你悬浮窗
/// </summary>
public partial class MiniWindow : Window
{
    private bool _opacityInitialized;

    /// <summary>当前显示的指标（本月 / 今日 / 当前小时）</summary>
    private MiniMetric _metric = MiniMetric.Month;

    /// <summary>最近一次快照：切换指标时立即重绘，不用等下一个 tick</summary>
    private SalarySnapshot? _lastSnapshot;

    public MiniWindow()
    {
        InitializeComponent();

        _metric = AppServices.Config.Settings.Mini.Metric;
        RestorePosition();
        SubscribeSnapshot();

        // 窗口移动结束后保存位置
        LocationChanged += OnLocationChanged;
        Closed += OnClosed;
    }

    /// <summary>
    /// 恢复上次位置与透明度（并做屏幕边界保护）
    /// </summary>
    private void RestorePosition()
    {
        var mini = AppServices.Config.Settings.Mini;

        // 透明度
        var opacity = Math.Clamp(mini.Opacity, 0.3, 1.0);
        OpacitySlider.Value = opacity;
        Opacity = opacity;

        // 位置（clamp 到虚拟屏幕内，防止拔掉显示器后窗口失联）
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualWidth = SystemParameters.VirtualScreenWidth;
        var virtualHeight = SystemParameters.VirtualScreenHeight;

        var left = Math.Clamp(mini.Left, virtualLeft, virtualLeft + virtualWidth - Width);
        var top = Math.Clamp(mini.Top, virtualTop, virtualTop + virtualHeight - Height);

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;

        _opacityInitialized = true;
    }

    /// <summary>
    /// 订阅实时快照
    /// </summary>
    private void SubscribeSnapshot()
    {
        AppServices.Ticker.Snapshot += OnSnapshot;

        // 立即显示当前值
        var current = AppServices.Ticker.Current;
        if (current != null)
            ApplySnapshot(current);
    }

    /// <summary>
    /// 应用快照
    /// </summary>
    private void OnSnapshot(object? sender, SalarySnapshot snapshot)
    {
        Dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(SalarySnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        // 标题与金额都跟着当前指标走（图标与主界面卡片保持一致）
        MetricText.Text = _metric switch
        {
            MiniMetric.Today => "☀️ 今日已赚",
            MiniMetric.Hour => "⏱️ 当前小时已赚",
            _ => "💰 本月已赚"
        };

        var amount = _metric switch
        {
            MiniMetric.Today => snapshot.TodayEarned,
            MiniMetric.Hour => snapshot.HourEarned,
            _ => snapshot.MonthEarned
        };
        AmountText.Text = $"¥{amount:N2}";

        StatusText.Text = snapshot.Status switch
        {
            WorkStatus.Earning => "赚钱中 💪",
            WorkStatus.LunchBreak => "午休中 😴",
            WorkStatus.OffWork => "已下班 👋",
            WorkStatus.ForcedRest => "强制休息 🌴",
            _ => "未开工 ☕"
        };
    }

    /// <summary>
    /// 切换显示内容：本月已赚 → 今日已赚 → 当前小时已赚 → 循环
    /// </summary>
    private void SwitchMetric_Click(object sender, RoutedEventArgs e)
    {
        _metric = _metric switch
        {
            MiniMetric.Month => MiniMetric.Today,
            MiniMetric.Today => MiniMetric.Hour,
            _ => MiniMetric.Month
        };

        // 记住选择，下次打开迷你窗保持一致
        AppServices.Config.Settings.Mini.Metric = _metric;
        AppServices.Config.SaveSettings();

        // 立即按新指标重绘，不等下一个 tick
        if (_lastSnapshot != null) ApplySnapshot(_lastSnapshot);
    }

    /// <summary>
    /// 拖拽移动窗口
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>
    /// 双击返回主窗口
    /// </summary>
    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ReturnToMain();
    }

    /// <summary>
    /// 透明度滑杆变化：实时应用并保存
    /// </summary>
    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;

        if (!_opacityInitialized) return;

        AppServices.Config.Settings.Mini.Opacity = e.NewValue;
        AppServices.Config.SaveSettings();
    }

    /// <summary>
    /// 位置变化：防抖保存
    /// </summary>
    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (!_opacityInitialized) return;

        AppServices.Config.Settings.Mini.Left = Left;
        AppServices.Config.Settings.Mini.Top = Top;
        AppServices.Config.SaveSettings();
    }

    /// <summary>
    /// 返回按钮
    /// </summary>
    private void Return_Click(object sender, RoutedEventArgs e)
    {
        ReturnToMain();
    }

    /// <summary>
    /// 返回主窗口（隐藏迷你窗，显示主窗）
    /// </summary>
    private void ReturnToMain()
    {
        foreach (Window window in Application.Current.Windows)
        {
            if (window is MainWindow main)
            {
                main.Show();
                main.Activate();
                break;
            }
        }
        Close();
    }

    /// <summary>
    /// 关闭：解除订阅
    /// </summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        AppServices.Ticker.Snapshot -= OnSnapshot;
    }
}
