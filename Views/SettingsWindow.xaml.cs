// ============================================================================
// 文件名：SettingsWindow.xaml.cs
// 功能描述：设置窗口代码后置
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SalaryTicker.Services;
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
        Loaded += (_, _) => WindowFit.Fit(this);

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

    // ========== 数值输入过滤（见 XAML 里 TextBoxNumber 样式） ==========

    /// <summary>
    /// 只放行数字与一个小数点（判断逻辑在 NumericInput，便于单测）
    /// </summary>
    private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !NumericInput.IsAcceptable((sender as TextBox)?.Text, e.Text);
    }

    /// <summary>
    /// 粘贴同样过滤：整段文本要么全合法，要么整段拒绝
    /// </summary>
    private void NumberOnly_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(typeof(string)) as string;
        if (!NumericInput.IsAcceptable((sender as TextBox)?.Text, text))
            e.CancelCommand();
    }
}
