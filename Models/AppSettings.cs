// 文件名：AppSettings.cs
// 描述：主设置类，聚合所有配置项

namespace SalaryTicker.Models;

/// <summary>
/// 应用主设置（可序列化 / 持久化）
/// </summary>
public class AppSettings
{
    // ========== 基本模式 ==========
    /// <summary>工资计算模式（税前 / 税后）</summary>
    public SalaryMode Mode { get; set; } = SalaryMode.Gross;
    /// <summary>税前工资</summary>
    public decimal GrossSalary { get; set; } = 20000m;
    /// <summary>税后工资</summary>
    public decimal NetSalary { get; set; } = 15000m;

    // ========== 养老保险 ==========
    /// <summary>养老输入模式</summary>
    public InsuranceInputMode PensionInputMode { get; set; } = InsuranceInputMode.Auto;
    /// <summary>养老基数</summary>
    public decimal PensionBase { get; set; }
    /// <summary>养老金额（手动）</summary>
    public decimal PensionAmount { get; set; }

    // ========== 医疗保险 ==========
    /// <summary>医保档次</summary>
    public MedicalTier MedicalTier { get; set; } = MedicalTier.Tier1;
    /// <summary>医保输入模式</summary>
    public InsuranceInputMode MedicalInputMode { get; set; } = InsuranceInputMode.Auto;
    /// <summary>医保基数</summary>
    public decimal MedicalBase { get; set; }
    /// <summary>医保金额（手动）</summary>
    public decimal MedicalAmount { get; set; }

    // ========== 失业保险 ==========
    /// <summary>失业输入模式</summary>
    public InsuranceInputMode UnemploymentInputMode { get; set; } = InsuranceInputMode.Auto;
    /// <summary>失业基数</summary>
    public decimal UnemploymentBase { get; set; }
    /// <summary>失业金额（手动）</summary>
    public decimal UnemploymentAmount { get; set; }

    // ========== 公积金 ==========
    /// <summary>公积金计算模式</summary>
    public HousingFundMode HousingFundMode { get; set; } = HousingFundMode.BaseRate;
    /// <summary>公积金金额（手动）</summary>
    public decimal HousingFundAmount { get; set; }
    /// <summary>公积金基数</summary>
    public decimal HousingFundBase { get; set; }
    /// <summary>公积金比例</summary>
    public decimal HousingFundRate { get; set; } = 0.05m;
    /// <summary>公积金封顶金额</summary>
    public decimal HousingFundCap { get; set; }

    // ========== 其他扣款 ==========
    /// <summary>其他扣款列表</summary>
    public List<OtherDeduction> OtherDeductions { get; set; } = new();

    // ========== 个税 ==========
    /// <summary>当前月份（用于累计预扣）</summary>
    public int CurrentMonth { get; set; } = DateTime.Now.Month;
    /// <summary>专项附加扣除</summary>
    public decimal SpecialDeduction { get; set; }

    // ========== 政策参数 ==========
    /// <summary>社保参数</summary>
    public SocialSecurityParams SocialSecurity { get; set; } = new();
    /// <summary>个税参数</summary>
    public TaxParams Tax { get; set; } = new();
    /// <summary>班次设置</summary>
    public ShiftSettings Shift { get; set; } = new();

    // ========== 节假日与补班 ==========
    /// <summary>节假日列表（强制休息，不计薪）</summary>
    public List<HolidayEntry> HolidayDates { get; set; } = new();

    /// <summary>补班日列表（国家规定的调休补班日，即使是周末也正常上班计薪）</summary>
    public List<HolidayEntry> MakeupWorkDays { get; set; } = new();

    // ========== 摸鱼 ==========
    /// <summary>摸鱼按钮配置</summary>
    public List<MuyuButtonSettings> MuyuButtons { get; set; } = new();
    /// <summary>是否启用摸鱼历史</summary>
    public bool MuyuHistoryEnabled { get; set; } = true;

    // ========== UI ==========
    /// <summary>是否显示启动画面</summary>
    public bool ShowSplashOnStartup { get; set; } = true;
    /// <summary>主题</summary>
    public string Theme { get; set; } = "Dark";
    /// <summary>迷你窗设置</summary>
    public MiniWindowSettings Mini { get; set; } = new();

    // ========== 更新 ==========
    /// <summary>是否检查新版本（关闭后应用完全不联网）</summary>
    public bool UpdateCheckEnabled { get; set; } = true;

    /// <summary>版本清单地址（为空表示未配置，不检查更新）</summary>
    public string UpdateCheckUrl { get; set; } = "";

    /// <summary>用户选择"忽略此版本"的版本号（不再提示该版本）</summary>
    public string SkippedVersion { get; set; } = "";

    /// <summary>上次检查更新的时间（UTC，用于 6 小时节流）</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }

    /// <summary>最后保存时间</summary>
    public DateTime LastSaved { get; set; } = DateTime.Now;
}
