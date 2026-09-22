// 文件名：SettingsDefaults.cs
// 描述：默认值工厂（深圳 2026 参数、7 级税率、班制预设、节假日、摸鱼按钮）

namespace SalaryTicker.Services;

using SalaryTicker.Models;

/// <summary>
/// 班制预设枚举
/// </summary>
public enum ShiftPreset
{
    /// <summary>五天八小时双休</summary>
    FiveDayEightHour,
    /// <summary>大小周</summary>
    AlternatingWeek,
    /// <summary>六天工作制</summary>
    SixDay,
    /// <summary>每日十小时</summary>
    TenHourDay
}

/// <summary>
/// 默认设置工厂
/// </summary>
public static class SettingsDefaults
{
    /// <summary>
    /// 创建带完整默认值的应用设置
    /// </summary>
    public static AppSettings Create()
    {
        var settings = new AppSettings
        {
            SocialSecurity = new SocialSecurityParams(),
            Tax = CreateTaxParams(),
            Shift = Preset(ShiftPreset.FiveDayEightHour),
            HolidayDates = Holidays2026(),
            MakeupWorkDays = MakeupWorkDays2026(),
            UpdateCheckUrl = "https://salaryticker.leelab.com.cn/version.json",
            HousingFundBase = 20000m,   // 默认公积金基数 = 默认税前工资
            MuyuButtons = new()
            {
                new() { Label = "去趟 WC", Emoji = "🚽" },
                new() { Label = "倒杯水", Emoji = "☕" },
                new() { Label = "抽根烟", Emoji = "🚬" }
            }
        };
        return settings;
    }

    /// <summary>
    /// 创建 7 级累进税率表（中国个税）
    /// </summary>
    private static TaxParams CreateTaxParams()
    {
        return new TaxParams
        {
            Brackets = new()
            {
                new() { UpperLimit = 36000,  Rate = 0.03m, QuickDeduction = 0 },
                new() { UpperLimit = 144000, Rate = 0.10m, QuickDeduction = 2520 },
                new() { UpperLimit = 300000, Rate = 0.20m, QuickDeduction = 16920 },
                new() { UpperLimit = 420000, Rate = 0.25m, QuickDeduction = 31920 },
                new() { UpperLimit = 660000, Rate = 0.30m, QuickDeduction = 52920 },
                new() { UpperLimit = 960000, Rate = 0.35m, QuickDeduction = 85920 },
                new() { UpperLimit = null,   Rate = 0.45m, QuickDeduction = 181920 }
            }
        };
    }

    /// <summary>
    /// 获取当前周的周一日期（正确处理周日）
    /// </summary>
    private static DateTime GetCurrentWeekMonday()
    {
        // .NET 中 DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
        // 需要转换为周一=0, 周二=1, ..., 周日=6 的偏移量
        var offset = ((int)DateTime.Now.DayOfWeek + 6) % 7;
        return DateTime.Now.AddDays(-offset);
    }

    /// <summary>
    /// 按预设创建班次设置
    /// </summary>
    public static ShiftSettings Preset(ShiftPreset preset)
    {
        return preset switch
        {
            ShiftPreset.FiveDayEightHour => new ShiftSettings
            {
                PresetName = "五天八小时双休",
                WorkStart = new(9, 0, 0),
                WorkEnd = new(18, 0, 0),
                LunchBreaks = new() { new() { Start = new(12, 0, 0), End = new(13, 0, 0) } },
                WorkDays = new()
                {
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                    DayOfWeek.Thursday, DayOfWeek.Friday
                },
                AlternateWeekEnabled = false
            },
            ShiftPreset.AlternatingWeek => new ShiftSettings
            {
                PresetName = "大小周",
                WorkStart = new(9, 0, 0),
                WorkEnd = new(18, 0, 0),
                LunchBreaks = new() { new() { Start = new(12, 0, 0), End = new(13, 0, 0) } },
                WorkDays = new()
                {
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                    DayOfWeek.Thursday, DayOfWeek.Friday
                },
                AltWorkDays = new()
                {
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                    DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
                },
                AlternateWeekEnabled = true,
                AnchorWeekMonday = DateOnly.FromDateTime(GetCurrentWeekMonday()),
                AnchorWeekIsBig = true
            },
            ShiftPreset.SixDay => new ShiftSettings
            {
                PresetName = "六天工作制",
                WorkStart = new(9, 0, 0),
                WorkEnd = new(18, 0, 0),
                LunchBreaks = new() { new() { Start = new(12, 0, 0), End = new(13, 0, 0) } },
                WorkDays = new()
                {
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                    DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
                },
                AlternateWeekEnabled = false
            },
            ShiftPreset.TenHourDay => new ShiftSettings
            {
                PresetName = "每日十小时",
                WorkStart = new(9, 0, 0),
                WorkEnd = new(20, 0, 0),
                LunchBreaks = new() { new() { Start = new(12, 0, 0), End = new(13, 0, 0) } },
                WorkDays = new()
                {
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                    DayOfWeek.Thursday, DayOfWeek.Friday
                },
                DailyPayHours = 10m,
                AlternateWeekEnabled = false
            },
            _ => new ShiftSettings()
        };
    }

    /// <summary>
    /// 2026 年法定节假日（含推定）
    /// </summary>
    public static List<HolidayEntry> Holidays2026()
    {
        return new()
        {
            // 元旦
            new() { Date = new(2026, 1, 1),  Name = "元旦" },
            new() { Date = new(2026, 1, 2),  Name = "元旦" },
            new() { Date = new(2026, 1, 3),  Name = "元旦" },

            // 春节（推定）
            new() { Date = new(2026, 2, 15), Name = "春节" },
            new() { Date = new(2026, 2, 16), Name = "春节" },
            new() { Date = new(2026, 2, 17), Name = "春节" },
            new() { Date = new(2026, 2, 18), Name = "春节" },
            new() { Date = new(2026, 2, 19), Name = "春节" },
            new() { Date = new(2026, 2, 20), Name = "春节" },
            new() { Date = new(2026, 2, 21), Name = "春节" },
            new() { Date = new(2026, 2, 22), Name = "春节" },

            // 清明节
            new() { Date = new(2026, 4, 4),  Name = "清明节" },
            new() { Date = new(2026, 4, 5),  Name = "清明节" },
            new() { Date = new(2026, 4, 6),  Name = "清明节" },

            // 劳动节（推定）
            new() { Date = new(2026, 5, 1),  Name = "劳动节" },
            new() { Date = new(2026, 5, 2),  Name = "劳动节" },
            new() { Date = new(2026, 5, 3),  Name = "劳动节" },
            new() { Date = new(2026, 5, 4),  Name = "劳动节" },
            new() { Date = new(2026, 5, 5),  Name = "劳动节" },

            // 端午节
            new() { Date = new(2026, 6, 19), Name = "端午节" },
            new() { Date = new(2026, 6, 20), Name = "端午节" },
            new() { Date = new(2026, 6, 21), Name = "端午节" },

            // 中秋节
            new() { Date = new(2026, 9, 25), Name = "中秋节" },
            new() { Date = new(2026, 9, 26), Name = "中秋节" },
            new() { Date = new(2026, 9, 27), Name = "中秋节" },

            // 国庆节（推定）
            new() { Date = new(2026, 10, 1), Name = "国庆节" },
            new() { Date = new(2026, 10, 2), Name = "国庆节" },
            new() { Date = new(2026, 10, 3), Name = "国庆节" },
            new() { Date = new(2026, 10, 4), Name = "国庆节" },
            new() { Date = new(2026, 10, 5), Name = "国庆节" },
            new() { Date = new(2026, 10, 6), Name = "国庆节" },
            new() { Date = new(2026, 10, 7), Name = "国庆节" }
        };
    }

    /// <summary>
    /// 2026 年调休补班日（推定）：国家规定的补班日，即使是周末也正常上班计薪
    /// 注：连休与补班安排为推定值，请以国务院办公厅通知为准，用户可在设置中增删
    /// </summary>
    public static List<HolidayEntry> MakeupWorkDays2026()
    {
        return new()
        {
            new() { Date = new(2026, 1, 4),   Name = "元旦补班" },
            new() { Date = new(2026, 2, 14),  Name = "春节补班" },
            new() { Date = new(2026, 2, 28),  Name = "春节补班" },
            new() { Date = new(2026, 5, 9),   Name = "劳动节补班" },
            new() { Date = new(2026, 9, 20),  Name = "国庆补班" },
            new() { Date = new(2026, 10, 10), Name = "国庆补班" }
        };
    }
}
