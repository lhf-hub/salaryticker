// ============================================================================
// 文件名：OffWorkReminderLogic.cs
// 功能描述：下班提醒的纯逻辑（到点边沿判定 + 每天只响一次，便于单元测试）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;

/// <summary>
/// 下班提醒纯逻辑
/// </summary>
public static class OffWorkReminderLogic
{
    /// <summary>
    /// 是否构成"刚下班"的触发边沿：上一帧有效且不是下班，这一帧是下班。
    /// 首帧（previous 为 null）不触发 —— 启动时已经过了下班时间不该响。
    /// </summary>
    public static bool IsOffWorkEdge(WorkStatus? previous, WorkStatus current)
        => previous.HasValue && previous.Value != WorkStatus.OffWork && current == WorkStatus.OffWork;
}

/// <summary>
/// 下班提醒判定器（有状态但无副作用）
/// 每秒都会喂一帧，而 RefreshNow / 暂停恢复会重复发同一状态的快照，
/// 所以必须"边沿触发 + 每天去重"，不能"看到 OffWork 就响"。
/// </summary>
public sealed class OffWorkReminderDetector
{
    private WorkStatus? _previousStatus;
    private DateOnly? _lastTriggeredDate;

    /// <summary>
    /// 喂入一帧状态，返回本次是否应触发提醒
    /// </summary>
    public bool Observe(DateOnly date, WorkStatus status)
    {
        var previous = _previousStatus;
        _previousStatus = status;

        if (!OffWorkReminderLogic.IsOffWorkEdge(previous, status)) return false;
        if (_lastTriggeredDate == date) return false;   // 每天只响一次

        _lastTriggeredDate = date;
        return true;
    }
}
