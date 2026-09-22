// ============================================================================
// 文件名：TimeSpanJsonConverter.cs
// 功能描述：System.Text.Json 的 TimeSpan 自定义转换器
// ============================================================================

namespace SalaryTicker.Converters;

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// TimeSpan 类型的 JSON 转换器
/// </summary>
/// <remarks>
/// 将 TimeSpan 序列化为 "hh:mm:ss" 格式的字符串
/// 从 "hh:mm:ss" 格式的字符串反序列化为 TimeSpan
/// </remarks>
public sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    private const string Format = @"hh\:mm\:ss";

    /// <summary>
    /// 从 JSON 读取 TimeSpan
    /// </summary>
    /// <param name="reader">JSON 读取器</param>
    /// <param name="typeToConvert">要转换的类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>解析后的 TimeSpan 值</returns>
    /// <exception cref="JsonException">无法解析 TimeSpan 时抛出</exception>
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (TimeSpan.TryParseExact(str, Format, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new JsonException($"无法解析 TimeSpan: {str}");
    }

    /// <summary>
    /// 将 TimeSpan 写入 JSON
    /// </summary>
    /// <param name="writer">JSON 写入器</param>
    /// <param name="value">要写入的 TimeSpan 值</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
