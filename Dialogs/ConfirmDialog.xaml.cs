// ============================================================================
// 文件名：ConfirmDialog.xaml.cs
// 功能描述：深色自定义确认对话框代码后置
// ============================================================================

using System.Windows;
using System.Windows.Media;

namespace SalaryTicker.Dialogs;

/// <summary>
/// 确认对话框（深色样式，替代 Win32 MessageBox）
/// </summary>
public partial class ConfirmDialog : Window
{
    internal ConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 显示确认对话框
    /// </summary>
    /// <param name="owner">父窗口</param>
    /// <param name="message">提示文本</param>
    /// <param name="title">标题</param>
    /// <param name="isDanger">是否为危险操作（确定按钮变红色）</param>
    /// <returns>用户是否确认</returns>
    public static bool Show(Window? owner, string message, string title = "确认操作", bool isDanger = false)
    {
        var dialog = new ConfirmDialog
        {
            Owner = owner
        };
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;

        if (isDanger)
        {
            dialog.OkButton.Style = (Style)dialog.FindResource("ButtonDanger");
        }

        return dialog.ShowDialog() == true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
