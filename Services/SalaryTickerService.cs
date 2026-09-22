// ============================================================================
// 文件名：SalaryTickerService.cs
// 功能描述：实时工资服务（PeriodicTimer每秒驱动，发事件）
// ============================================================================

namespace SalaryTicker.Services;

using SalaryTicker.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 实时工资服务
/// </summary>
public sealed class SalaryTickerService : IDisposable
{
    private readonly SalaryEngine _engine;
    private readonly TimeProvider _timeProvider;
    private readonly Func<AppSettings> _settingsProvider;
    private readonly int _intervalMs;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _tickerTask;
    private bool _paused;
    private SalarySnapshot? _currentSnapshot;

    /// <summary>
    /// 快照更新事件
    /// </summary>
    public event EventHandler<SalarySnapshot>? Snapshot;

    /// <summary>
    /// 当前快照
    /// </summary>
    public SalarySnapshot? Current => _currentSnapshot;

    /// <summary>
    /// 初始化实时工资服务
    /// </summary>
    /// <param name="engine">工资引擎</param>
    /// <param name="timeProvider">时间提供者</param>
    /// <param name="settingsProvider">设置提供者（每次 tick 动态获取最新设置，避免捕获旧引用）</param>
    /// <param name="intervalMs">tick 间隔（毫秒）</param>
    public SalaryTickerService(SalaryEngine engine, TimeProvider timeProvider, Func<AppSettings> settingsProvider, int intervalMs = 1000)
    {
        _engine = engine;
        _timeProvider = timeProvider;
        _settingsProvider = settingsProvider;
        _intervalMs = intervalMs;
    }

    /// <summary>
    /// 启动服务
    /// </summary>
    public void Start()
    {
        if (_timer != null) return;

        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
        _paused = false;

        _tickerTask = Task.Run(async () =>
        {
            try
            {
                while (await _timer.WaitForNextTickAsync(_cts.Token))
                {
                    if (_paused) continue;

                    var now = _timeProvider.GetLocalNow();
                    var snapshot = _engine.ComputeSnapshot(now, _settingsProvider());
                    _currentSnapshot = snapshot;
                    Snapshot?.Invoke(this, snapshot);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        });
    }

    /// <summary>
    /// 暂停服务
    /// </summary>
    public void Pause()
    {
        _paused = true;
    }

    /// <summary>
    /// 恢复服务
    /// </summary>
    public void Resume()
    {
        _paused = false;
    }

    /// <summary>
    /// 立即刷新（不等下一个 tick）
    /// </summary>
    public void RefreshNow()
    {
        var now = _timeProvider.GetLocalNow();
        var snapshot = _engine.ComputeSnapshot(now, _settingsProvider());
        _currentSnapshot = snapshot;
        Snapshot?.Invoke(this, snapshot);
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _timer = null;
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Stop();
    }
}
