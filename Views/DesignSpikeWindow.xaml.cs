// ============================================================================
// 文件名：DesignSpikeWindow.xaml.cs
// 功能描述：B 流水台账方向的 WPF 保真 spike（临时窗口，验证字体/等宽数字/DPI/focus）
// ============================================================================

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SalaryTicker.Views;

public partial class DesignSpikeWindow : Window
{
    public DesignSpikeWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLayout();

        var oneWidth = TabOne.ActualWidth;
        var zeroWidth = TabZero.ActualWidth;
        var delta = Math.Abs(oneWidth - zeroWidth);
        var tabularOk = delta < 0.5;
        TabStatus.Text = tabularOk
            ? $"等宽数字：通过（宽度差 {delta:F2} px）"
            : $"等宽数字：失败（宽度差 {delta:F2} px，逐秒刷新会抖动）";
        TabStatus.Foreground = (Brush)FindResource(tabularOk ? "BGreen" : "BRed");

        var dpi = VisualTreeHelper.GetDpi(this);
        DpiText.Text = $"DPI：{dpi.PixelsPerDip * 96:F0}%（PixelsPerDip {dpi.PixelsPerDip:F2}）";
        DiagText.Text = $"Window {ActualWidth:F0}×{ActualHeight:F0} | Root {RootGrid.ActualWidth:F0} | Panel {StatementPanel.ActualWidth:F0} | Hero {HeroAmount.ActualWidth:F0}";

        // 让首屏截图里能看到 focus ring（绿色 2px，4px 圆角）。
        FocusTarget.Focus();
        Keyboard.Focus(FocusTarget);
    }
}
