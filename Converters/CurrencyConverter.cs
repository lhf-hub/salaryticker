// ============================================================================
// 文件名：CurrencyConverter.cs
// 功能描述：货币格式化转换器（decimal → "¥12,345.67"）
// ============================================================================

namespace SalaryTicker.Converters;

using System;
using System.Globalization;
using System.Windows.Data;

/// <summary>
/// 货币格式化转换器
/// </summary>
public sealed class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal amount)
            return $"¥{amount:N2}";
        return "¥0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
