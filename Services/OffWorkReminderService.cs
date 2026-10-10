// ============================================================================
// 文件名：OffWorkReminderService.cs
// 功能描述：下班到点提醒（系统提示音循环，进程存活期间有效）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Media;
using System.Threading;

/// <summary>
/// 下班提醒服务
/// 订阅每秒快照，在"刚好跨过下班时间"时响铃（默认每 2 秒一次、共约 60 秒）。
/// 快照事件在后台线程触发，SystemSounds 在后台线程调用是安全的（底层是 MessageBeep）。
/// </summary>
public sealed class OffWorkReminderService : IDisposable
{
    private readonly SalaryTickerService _ticker;
    private readonly Func<AppSettings> _settingsProvider;
    private readonly Action _playSound;
    private readonly OffWorkReminderDetector _detector = new();
    private readonly TimeSpan _ringInterval;
    private readonly int _ringCount;
    private readonly object _gate = new();
    private Timer? _ringTimer;
    private int _remainingRings;
    private bool _disposed;

    /// <summary>当前是否正在响铃</summary>
    public bool IsRinging
    {
        get { lock (_gate) return _ringTimer != null; }
    }

    /// <summary>
    /// 初始化下班提醒服务（构造即订阅快照）
    /// </summary>
    /// <param name="ticker">实时工资服务</param>
    /// <param name="settingsProvider">设置提供者（每帧读取开关，关闭后立即停声）</param>
    /// <param name="playSound">发声动作（测试可注入；默认为系统提示音）</param>
    /// <param name="ringInterval">响铃间隔（默认 2 秒）</param>
    /// <param name="ringCount">响铃总次数（默认 30 次 ≈ 60 秒）</param>
    public OffWorkReminderService(
        SalaryTickerService ticker,
        Func<AppSettings> settingsProvider,
        Action? playSound = null,
        TimeSpan? ringInterval = null,
        int ringCount = 30)
    {
        _ticker = ticker;
        _settingsProvider = settingsProvider;
        _playSound = playSound ?? (() => SystemSounds.Exclamation.Play());
        _ringInterval = ringInterval ?? TimeSpan.FromSeconds(2);
        _ringCount = ringCount;
        _ticker.Snapshot += OnSnapshot;
    }

    /// <summary>
    /// 处理一帧快照（后台线程与 UI 线程都可能进来）
    /// </summary>
    private void OnSnapshot(object? sender, SalarySnapshot snapshot)
    {
        var enabled = _settingsProvider().OffWorkReminderEnabled;
        var today = DateOnly.FromDateTime(snapshot.Now.LocalDateTime);

        // 判定器始终喂状态：开关关着的时候错过的下班边沿，之后再打开也不会补响
        bool trigger;
        lock (_gate) trigger = _detector.Observe(today, snapshot.Status);

        if (!enabled)
        {
            StopRinging();
            return;
        }

        if (trigger) StartRinging();
    }

    /// <summary>
    /// 开始响铃（已经在响则忽略，重复快照不会重启）
    /// </summary>
    private void StartRinging()
    {
        lock (_gate)
        {
            if (_disposed || _ringTimer != null) return;

            _remainingRings = _ringCount;
            PlayOne();
            _ringTimer = new Timer(_ => OnRingTick(), null, _ringInterval, _ringInterval);
        }
    }

    /// <summary>
    /// 响铃节拍（Timer 回调里抛异常会直接终止进程，整段兜住）
    /// </summary>
    private void OnRingTick()
    {
        try
        {
            lock (_gate)
            {
                if (_disposed || _ringTimer == null) return;

                _remainingRings--;
                if (_remainingRings <= 0)
                {
                    StopRingingLocked();
                    return;
                }

                PlayOne();
            }
        }
        catch
        {
            // 提示音失败不影响主流程
        }
    }

    /// <summary>
    /// 响一声（无音频设备等情况不抛给上层）
    /// </summary>
    private void PlayOne()
    {
        try { _playSound(); }
        catch { /* 忽略 */ }
    }

    /// <summary>
    /// 停止响铃（线程安全，可随时调用）
    /// </summary>
    public void StopRinging()
    {
        lock (_gate) StopRingingLocked();
    }

    private void StopRingingLocked()
    {
        _ringTimer?.Dispose();
        _ringTimer = null;
        _remainingRings = 0;
    }

    /// <summary>
    /// 释放：停声并退订快照
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            StopRingingLocked();
        }
        _ticker.Snapshot -= OnSnapshot;
    }
}
