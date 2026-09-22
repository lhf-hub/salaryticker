// ============================================================================
// 文件名：BoolToVisibilityConverter.cs
// 功能描述：布尔转可见性转换器（true → Visible, false → Collapsed）
// ============================================================================

namespace SalaryTicker.Converters;

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

/// <summary>
/// 布尔转可见性转换器
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v == Visibility.Visible;
        return false;
    }
}
