// ============================================================================
// 文件名：SettingsWindow.xaml.cs
// 功能描述：设置窗口代码后置
// ============================================================================

using System.Windows;
using SalaryTicker.ViewModels;

namespace SalaryTicker.Views;

/// <summary>
/// 设置窗口
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    /// <summary>
    /// 设置窗口
    /// </summary>
    /// <param name="updateViewModel">主界面的更新状态（共享同一实例，保证"立即检查更新"与提示条同步）</param>
    public SettingsWindow(UpdateViewModel? updateViewModel = null)
    {
        InitializeComponent();

        _viewModel = new SettingsViewModel(updateViewModel);
        DataContext = _viewModel;

        _viewModel.Saved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        _viewModel.Cancelled += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
    }
}
