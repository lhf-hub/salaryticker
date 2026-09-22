// ============================================================================
// 文件名：InverseBoolToVisibilityConverter.cs
// 功能描述：反向布尔转可见性（true → Collapsed，false → Visible）
//           用于"下载中隐藏操作按钮"这类场景
// ============================================================================

namespace SalaryTicker.Converters;

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

/// <summary>
/// 反向布尔转可见性转换器
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v != Visibility.Visible;
        return false;
    }
}
