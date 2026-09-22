// ============================================================================
// 文件名：MuyuHistoryService.cs
// 功能描述：摸鱼历史服务，管理摸鱼记录的持久化和统计
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 摸鱼统计结果
/// </summary>
public sealed class MuyuStats
{
    /// <summary>今日次数</summary>
    public int TodayCount { get; set; }
    /// <summary>今日总秒数</summary>
    public int TodayTotalSeconds { get; set; }
    /// <summary>今日总成本</summary>
    public decimal TodayTotalCost { get; set; }
    /// <summary>本周次数</summary>
    public int WeekCount { get; set; }
    /// <summary>本周总秒数</summary>
    public int WeekTotalSeconds { get; set; }
    /// <summary>本周总成本</summary>
    public decimal WeekTotalCost { get; set; }
    /// <summary>本月次数</summary>
    public int MonthCount { get; set; }
    /// <summary>本月总秒数</summary>
    public int MonthTotalSeconds { get; set; }
    /// <summary>本月总成本</summary>
    public decimal MonthTotalCost { get; set; }
}

/// <summary>
/// 摸鱼历史文档（用于 JSON 序列化）
/// </summary>
public sealed class MuyuHistoryDocument
{
    /// <summary>摸鱼记录列表</summary>
    public List<MuyuRecord> Records { get; set; } = new();
}

/// <summary>
/// 摸鱼历史服务类
/// 负责摸鱼记录的增删和统计计算
/// </summary>
public sealed class MuyuHistoryService
{
    private readonly JsonFileStore<MuyuHistoryDocument> _store;
    private MuyuHistoryDocument _document;

    /// <summary>
    /// 只读的摸鱼记录列表
    /// </summary>
    public IReadOnlyList<MuyuRecord> Records => _document.Records;

    /// <summary>
    /// 初始化摸鱼历史服务
    /// </summary>
    /// <param name="appDataDir">应用数据目录</param>
    public MuyuHistoryService(string appDataDir)
    {
        var backupDir = Path.Combine(appDataDir, "backup");
        var options = JsonFileStore<MuyuHistoryDocument>.CreateDefaultOptions();
        _store = new JsonFileStore<MuyuHistoryDocument>(
            Path.Combine(appDataDir, "muyu-history.json"), backupDir, options);

        var result = _store.Load();
        _document = result.Data;
    }

    /// <summary>
    /// 添加一条摸鱼记录
    /// </summary>
    /// <param name="record">摸鱼记录</param>
    public void Append(MuyuRecord record)
    {
        _document.Records.Add(record);
        _store.Save(_document);
    }

    /// <summary>
    /// 清空所有摸鱼记录
    /// </summary>
    public void ClearAll()
    {
        _document.Records.Clear();
        _store.Save(_document);
    }

    /// <summary>
    /// 获取摸鱼统计数据
    /// </summary>
    /// <param name="today">当前日期</param>
    /// <returns>统计数据</returns>
    public MuyuStats GetStats(DateOnly today)
    {
        var stats = new MuyuStats();

        // 计算本周一的日期（周一为一周的开始）
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-daysSinceMonday);

        foreach (var record in _document.Records)
        {
            // 今日统计
            if (record.Date == today)
            {
                stats.TodayCount++;
                stats.TodayTotalSeconds += record.DurationSeconds;
                stats.TodayTotalCost += record.CostAmount;
            }

            // 本周统计（从周一到今天）
            if (record.Date >= monday && record.Date <= today)
            {
                stats.WeekCount++;
                stats.WeekTotalSeconds += record.DurationSeconds;
                stats.WeekTotalCost += record.CostAmount;
            }

            // 本月统计
            if (record.Date.Year == today.Year && record.Date.Month == today.Month)
            {
                stats.MonthCount++;
                stats.MonthTotalSeconds += record.DurationSeconds;
                stats.MonthTotalCost += record.CostAmount;
            }
        }

        return stats;
    }
}
