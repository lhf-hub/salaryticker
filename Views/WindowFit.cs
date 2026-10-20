// ============================================================================
// 文件名：WindowFit.cs
// 功能描述：按「窗口所在显示器的物理工作区 ÷ 窗口 DPI」限制尺寸并居中。
//           不能用 SystemParameters.WorkArea：它在 125% 缩放下会给出错误的高 DPI 逻辑值。
// ============================================================================

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SalaryTicker.Views;

internal static class WindowFit
{
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>
    /// 限制窗口不超出所在显示器的工作区，并在该工作区内居中。
    /// </summary>
    public static void Fit(Window window, double margin = 24)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        window.UpdateLayout();

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
            return;

        // Loaded 之后 CompositionTarget 的缩放才是窗口实际缩放；再退回 VisualTreeHelper / Win32。
        var scale = 0.0;
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget != null)
            scale = source.CompositionTarget.TransformToDevice.M11;
        if (scale <= 0)
            scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        if (scale <= 1.0)
        {
            var windowDpi = GetDpiForWindow(handle) / 96.0;
            if (windowDpi > 0)
                scale = windowDpi;
        }

        var workWidth = (info.rcWork.Right - info.rcWork.Left) / scale;
        var workHeight = (info.rcWork.Bottom - info.rcWork.Top) / scale;
        var availableWidth = Math.Max(320, workWidth - margin * 2);
        var availableHeight = Math.Max(240, workHeight - margin * 2);

        // WPF enforces MinWidth/MinHeight over Width/Height. Lower the minimum
        // first so a small or HiDPI work area cannot leave the window off-screen.
        if (window.MinWidth > availableWidth) window.MinWidth = availableWidth;
        if (window.MinHeight > availableHeight) window.MinHeight = availableHeight;

        window.Width = Math.Min(window.Width, availableWidth);
        window.Height = Math.Min(window.Height, availableHeight);
        window.UpdateLayout();

        var actualWidth = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var actualHeight = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        window.Left = info.rcWork.Left / scale + (workWidth - actualWidth) / 2;
        window.Top = info.rcWork.Top / scale + (workHeight - actualHeight) / 2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);
}
