// 文件名：MiniWindowSettings.cs
// 描述：迷你窗设置

namespace SalaryTicker.Models;

/// <summary>
/// 迷你窗（悬浮窗）设置
/// </summary>
public class MiniWindowSettings
{
    /// <summary>距屏幕左侧距离</summary>
    public double Left { get; set; } = 100;
    /// <summary>距屏幕顶部距离</summary>
    public double Top { get; set; } = 100;
    /// <summary>不透明度</summary>
    public double Opacity { get; set; } = 0.9;

    /// <summary>显示哪个指标（本月 / 今日 / 当前小时），可用 ⇄ 按钮切换</summary>
    public MiniMetric Metric { get; set; } = MiniMetric.Month;
}
