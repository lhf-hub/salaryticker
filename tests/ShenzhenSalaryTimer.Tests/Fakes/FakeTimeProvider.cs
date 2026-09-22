// ============================================================================
// 文件名：FakeTimeProvider.cs
// 功能描述：用于单元测试的假时间提供者，支持手动控制时间
// ============================================================================

namespace ShenzhenSalaryTimer.Tests.Fakes;

using System;

/// <summary>
/// 假时间提供者
/// 用于测试中精确控制时间，避免依赖系统时间
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    /// <summary>
    /// 初始化假时间提供者
    /// </summary>
    /// <param name="initialTime">初始时间</param>
    public FakeTimeProvider(DateTimeOffset initialTime)
    {
        _now = initialTime;
    }

    /// <summary>
    /// 获取当前 UTC 时间
    /// </summary>
    /// <returns>当前 UTC 时间</returns>
    public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();

    /// <summary>
    /// 将时间前进指定时长
    /// </summary>
    /// <param name="duration">要前进的时长</param>
    public void Advance(TimeSpan duration)
    {
        _now = _now.Add(duration);
    }

    /// <summary>
    /// 设置指定时间
    /// </summary>
    /// <param name="time">要设置的时间</param>
    public void Set(DateTimeOffset time)
    {
        _now = time;
    }
}
