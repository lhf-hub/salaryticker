// ============================================================================
// 文件名：InverseBoolConverter.cs
// 功能描述：布尔取反转换器
// ============================================================================

namespace SalaryTicker.Converters;

using System;
using System.Globalization;
using System.Windows.Data;

/// <summary>
/// 布尔取反转换器
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}
