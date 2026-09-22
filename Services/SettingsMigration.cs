// ============================================================================
// 文件名：SettingsMigration.cs
// 功能描述：设置数据的向前修正（把历史版本里写错的内置数据就地改对）
//
// 说明：应用优先使用用户已保存的配置，所以内置默认值改对后，老用户不会自动
//       跟着变。这里针对"确实是我们写错的数据"做定向修正，避免让用户手动改。
//       只做点对点修正，不做通用迁移引擎。
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;

/// <summary>
/// 设置数据修正
/// </summary>
public static class SettingsMigration
{
    /// <summary>
    /// 对加载后的设置做一次修正
    /// </summary>
    /// <returns>是否发生了改动（调用方据此决定是否立即落盘）</returns>
    public static bool Apply(AppSettings settings)
    {
        var changed = false;
        changed |= FixNationalDayMakeupDayLabel(settings);
        return changed;
    }

    /// <summary>
    /// 2026-09-20 在早期版本中被误标为「中秋补班」，实际是国庆节补班
    /// （国庆有两个补班日：9/20 与 10/10）
    /// </summary>
    private static bool FixNationalDayMakeupDayLabel(AppSettings settings)
    {
        var target = new DateOnly(2026, 9, 20);
        var changed = false;

        foreach (var entry in settings.MakeupWorkDays)
        {
            if (entry.Date == target && entry.Name == "中秋补班")
            {
                entry.Name = "国庆补班";
                changed = true;
            }
        }

        return changed;
    }
}
