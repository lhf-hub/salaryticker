// WarmGalleryWindow.xaml.cs —— Warm 主题对照窗代码后置（纯视图，零业务逻辑）
using System.Windows;

namespace SalaryTicker.Views;

/// <summary>
/// Warm 主题对照窗：验证官网暖色高级版的字体、配色、圆角与语义色在 WPF 中的观感。
/// 启动方式：SalaryTicker.exe warm 或 dotnet run -- warm
/// </summary>
public partial class WarmGalleryWindow : Window
{
    public WarmGalleryWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => WindowFit.Fit(this);
    }
}
