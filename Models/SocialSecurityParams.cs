// 文件名：SocialSecurityParams.cs
// 描述：社保政策参数（基数上下限 + 个人缴费比例）

namespace SalaryTicker.Models;

/// <summary>
/// 社保参数（深圳 2026 默认值）
/// </summary>
public class SocialSecurityParams
{
    // ========== 养老保险 ==========
    /// <summary>个人缴费比例（8%）</summary>
    public decimal PensionRate { get; set; } = 0.08m;
    /// <summary>基数下限</summary>
    public decimal PensionMin { get; set; } = 4775m;
    /// <summary>基数上限</summary>
    public decimal PensionMax { get; set; } = 27549m;

    // ========== 医疗保险 ==========
    /// <summary>一档个人比例（2%）</summary>
    public decimal MedicalTier1Rate { get; set; } = 0.02m;
    /// <summary>二档个人比例（0.5%）</summary>
    public decimal MedicalTier2Rate { get; set; } = 0.005m;
    /// <summary>基数下限</summary>
    public decimal MedicalMin { get; set; } = 6727m;
    /// <summary>基数上限</summary>
    public decimal MedicalMax { get; set; } = 33633m;

    // ========== 失业保险 ==========
    /// <summary>个人缴费比例（0.2%）</summary>
    public decimal UnemploymentRate { get; set; } = 0.002m;
    /// <summary>基数下限（深圳最低工资）</summary>
    public decimal UnemploymentMin { get; set; } = 2520m;
    /// <summary>基数上限</summary>
    public decimal UnemploymentMax { get; set; } = 44265m;
}
