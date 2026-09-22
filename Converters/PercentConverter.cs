// ============================================================================
// 文件名：PercentConverter.cs
// 功能描述：百分比格式化转换器（0.05 → "5%"）
// ============================================================================

namespace SalaryTicker.Converters;

using System;
using System.Globalization;
using System.Windows.Data;

/// <summary>
/// 百分比格式化转换器
/// </summary>
public sealed class PercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal percent)
            return $"{percent * 100:N0}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
