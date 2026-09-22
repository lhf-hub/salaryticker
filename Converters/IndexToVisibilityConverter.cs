// ============================================================================
// 文件名：IndexToVisibilityConverter.cs
// 功能描述：索引匹配转换器（SelectedIndex == ConverterParameter 时可见）
// ============================================================================

namespace SalaryTicker.Converters;

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

/// <summary>
/// 索引匹配转换器：当前索引等于参数时显示，否则折叠
/// </summary>
public sealed class IndexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && parameter != null &&
            int.TryParse(parameter.ToString(), out var target))
        {
            return index == target ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 索引大于等于参数时显示（用于"基数×比例"模式：索引 ≥ 1 时显示基数输入）
/// </summary>
public sealed class IndexMinToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && parameter != null &&
            int.TryParse(parameter.ToString(), out var min))
        {
            return index >= min ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
