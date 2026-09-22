// 文件名：Enums.cs
// 描述：全局枚举定义

namespace SalaryTicker.Models;

/// <summary>
/// 工资计算模式
/// </summary>
public enum SalaryMode
{
    /// <summary>税前</summary>
    Gross,
    /// <summary>税后</summary>
    Net
}

/// <summary>
/// 医疗保险档次
/// </summary>
public enum MedicalTier
{
    /// <summary>一档</summary>
    Tier1,
    /// <summary>二档</summary>
    Tier2
}

/// <summary>
/// 社保缴费基数的确定方式
/// 注意：成员顺序与设置界面下拉框索引一一对应，调整顺序需同步 SettingsWindow.xaml
/// </summary>
public enum InsuranceInputMode
{
    /// <summary>自动：以税前工资为基数（受社保上下限约束）</summary>
    Auto,
    /// <summary>按下限：直接使用缴费基数下限（常见于按最低基数申报的公司）</summary>
    MinBase,
    /// <summary>按上限：直接使用缴费基数上限</summary>
    MaxBase,
    /// <summary>自定义基数：手动指定（同样受上下限约束）</summary>
    Base,
    /// <summary>直接填月缴额：直接采用工资条上的扣款金额</summary>
    Amount
}

/// <summary>
/// 公积金计算模式
/// </summary>
public enum HousingFundMode
{
    /// <summary>手动指定金额</summary>
    Amount,
    /// <summary>基数×比例</summary>
    BaseRate,
    /// <summary>基数×比例，带封顶</summary>
    BaseRateCap
}

/// <summary>
/// 工作状态
/// </summary>
public enum WorkStatus
{
    /// <summary>强制休息（节假日/周末）</summary>
    ForcedRest,
    /// <summary>未开始工作</summary>
    NotStarted,
    /// <summary>正在工作</summary>
    Earning,
    /// <summary>午休</summary>
    LunchBreak,
    /// <summary>已下班</summary>
    OffWork
}

/// <summary>
/// 迷你悬浮窗显示的指标
/// </summary>
public enum MiniMetric
{
    /// <summary>本月已赚</summary>
    Month,
    /// <summary>今日已赚</summary>
    Today,
    /// <summary>当前小时已赚</summary>
    Hour
}

/// <summary>
/// 主视图切换
/// </summary>
public enum MainView
{
    /// <summary>仪表盘</summary>
    Dashboard,
    /// <summary>摸鱼历史</summary>
    MuyuHistory
}
