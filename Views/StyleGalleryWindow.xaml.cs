// StyleGalleryWindow.xaml.cs —— 样式画廊代码后置（纯视图，零业务逻辑）
using System.Windows;

namespace SalaryTicker.Views;

/// <summary>
/// 样式画廊窗口：集中展示设计系统全部控件样式，用于阶段验收与后续回归目检。
/// 启动方式：SalaryTicker.exe gallery 或 dotnet run -- gallery
/// </summary>
public partial class StyleGalleryWindow : Window
{
    public StyleGalleryWindow()
    {
        InitializeComponent();
    }
}