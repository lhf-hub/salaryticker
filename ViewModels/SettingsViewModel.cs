// ============================================================================
// 文件名：SettingsViewModel.cs
// 功能描述：设置窗口 ViewModel（草稿机制 + 保存确认流程）
// ============================================================================

namespace SalaryTicker.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SalaryTicker.Dialogs;
using SalaryTicker.Models;
using SalaryTicker.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

/// <summary>
/// 设置窗口 ViewModel
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private AppSettings _live;

    // ========== 页面导航 ==========

    /// <summary>当前设置页（0=工资五险一金 1=个税 2=班制 3=摸鱼 4=政策参数 5=关于）</summary>
    [ObservableProperty]
    private int _selectedPageIndex;

    // ========== 工资与五险一金 ==========

    /// <summary>是否税前模式</summary>
    [ObservableProperty]
    private bool _isGrossMode = true;

    /// <summary>是否税后模式（RadioButton 双向绑定用）</summary>
    public bool IsNetMode
    {
        get => !IsGrossMode;
        set => IsGrossMode = !value;
    }

    // ========== 实时计算说明（让"自动/基数/金额"三种模式一目了然） ==========

    /// <summary>养老保险计算说明</summary>
    public string PensionDetail => BuildInsuranceDetail(
        PensionInputModeIndex, PensionBase, PensionAmount, GrossSalary,
        PensionRatePercent / 100, PensionMin, PensionMax);

    /// <summary>医疗保险计算说明</summary>
    public string MedicalDetail
    {
        get
        {
            var rate = MedicalTierIndex == 0 ? MedicalTier1RatePercent : MedicalTier2RatePercent;
            var tier = MedicalTierIndex == 0 ? "深圳一档" : "深圳二档";
            return $"【{tier}】" + BuildInsuranceDetail(
                MedicalInputModeIndex, MedicalBase, MedicalAmount, GrossSalary,
                rate / 100, MedicalMin, MedicalMax);
        }
    }

    /// <summary>失业保险计算说明</summary>
    public string UnemploymentDetail => BuildInsuranceDetail(
        UnemploymentInputModeIndex, UnemploymentBase, UnemploymentAmount, GrossSalary,
        UnemploymentRatePercent / 100, UnemploymentMin, UnemploymentMax);

    /// <summary>公积金计算说明</summary>
    public string HousingFundDetail
    {
        get
        {
            var rate = HousingFundRate * 100;
            return HousingFundModeIndex switch
            {
                0 => $"直接采用 ¥{HousingFundAmount:N2}/月",
                1 => $"基数 {HousingFundBase:N0} × {rate:0.#}% = ¥{HousingFundBase * HousingFundRate:N2}/月",
                2 => BuildCapDetail(),
                _ => ""
            };
        }
    }

    /// <summary>公积金"基数+比例+上限"说明（体现先钳制基数再计算）</summary>
    private string BuildCapDetail()
    {
        var capped = Math.Min(HousingFundBase, HousingFundCap);
        var rate = HousingFundRate * 100;
        if (capped < HousingFundBase)
            return $"基数 {HousingFundBase:N0} 超过上限，按 {capped:N0} × {rate:0.#}% = ¥{capped * HousingFundRate:N2}/月";
        return $"基数 {HousingFundBase:N0} × {rate:0.#}% = ¥{capped * HousingFundRate:N2}/月";
    }

    /// <summary>构建单个险种的计算说明（自动 / 基数 / 金额）</summary>
    private static string BuildInsuranceDetail(
        int modeIndex, decimal baseInput, decimal amountInput, decimal grossSalary,
        decimal rate, decimal min, decimal max)
    {
        switch (modeIndex)
        {
            case 0: // 自动：以工资为基数，受社保上下限约束
                if (grossSalary >= min && grossSalary <= max)
                    return $"工资 {grossSalary:N0} × {rate:0.##%} = ¥{grossSalary * rate:N2}/月";
                var clamped = Math.Min(Math.Max(grossSalary, min), max);
                var relation = grossSalary < min ? "低于下限" : "高于上限";
                return $"工资 {grossSalary:N0} {relation}，按 {clamped:N0} × {rate:0.##%} = ¥{clamped * rate:N2}/月";

            case 1: // 按下限（最低基数申报）
                return $"按下限 {min:N0} × {rate:0.##%} = ¥{min * rate:N2}/月";

            case 2: // 按上限（最高基数申报）
                return $"按上限 {max:N0} × {rate:0.##%} = ¥{max * rate:N2}/月";

            case 3: // 自定义基数：同样受上下限约束
                if (baseInput >= min && baseInput <= max)
                    return $"基数 {baseInput:N0} × {rate:0.##%} = ¥{baseInput * rate:N2}/月";
                var cb = Math.Min(Math.Max(baseInput, min), max);
                var relation2 = baseInput < min ? "低于下限" : "高于上限";
                return $"基数 {baseInput:N0} {relation2}，按 {cb:N0} × {rate:0.##%} = ¥{cb * rate:N2}/月";

            default: // 直接填月缴额
                return $"直接采用 ¥{amountInput:N2}/月（不再按比例计算）";
        }
    }

    /// <summary>公积金比例（百分比显示，5 表示 5%）</summary>
    public decimal HousingFundRatePercent
    {
        get => HousingFundRate * 100;
        set => HousingFundRate = value / 100;
    }

    /// <summary>税前工资</summary>
    [ObservableProperty]
    private decimal _grossSalary = 20000m;

    /// <summary>税后工资</summary>
    [ObservableProperty]
    private decimal _netSalary = 15000m;

    // 养老
    [ObservableProperty] private int _pensionInputModeIndex;
    [ObservableProperty] private decimal _pensionBase;
    [ObservableProperty] private decimal _pensionAmount;

    // 医疗
    [ObservableProperty] private int _medicalTierIndex;
    [ObservableProperty] private int _medicalInputModeIndex;
    [ObservableProperty] private decimal _medicalBase;
    [ObservableProperty] private decimal _medicalAmount;

    // 失业
    [ObservableProperty] private int _unemploymentInputModeIndex;
    [ObservableProperty] private decimal _unemploymentBase;
    [ObservableProperty] private decimal _unemploymentAmount;

    // 公积金
    [ObservableProperty] private int _housingFundModeIndex = 1;
    [ObservableProperty] private decimal _housingFundAmount;
    [ObservableProperty] private decimal _housingFundBase;
    [ObservableProperty] private decimal _housingFundRate = 0.05m;
    [ObservableProperty] private decimal _housingFundCap;

    /// <summary>其他扣款列表</summary>
    public ObservableCollection<OtherDeduction> OtherDeductions { get; } = new();

    // ========== 个税 ==========

    /// <summary>当前月份（1-12）</summary>
    [ObservableProperty]
    private int _currentMonth = DateTime.Now.Month;

    /// <summary>专项附加扣除</summary>
    [ObservableProperty]
    private decimal _specialDeduction;

    // ========== 班制 ==========

    /// <summary>班制预设索引</summary>
    [ObservableProperty]
    private int _shiftPresetIndex;

    /// <summary>上班时间文本</summary>
    [ObservableProperty]
    private string _workStartText = "09:00";

    /// <summary>下班时间文本</summary>
    [ObservableProperty]
    private string _workEndText = "18:00";

    /// <summary>午休开始文本</summary>
    [ObservableProperty]
    private string _lunchStartText = "12:00";

    /// <summary>午休结束文本</summary>
    [ObservableProperty]
    private string _lunchEndText = "13:00";

    /// <summary>日计薪小时</summary>
    [ObservableProperty]
    private decimal _dailyPayHours = 8m;

    /// <summary>是否启用大小周</summary>
    [ObservableProperty]
    private bool _alternateWeekEnabled;

    /// <summary>锚点周是否大周</summary>
    [ObservableProperty]
    private bool _anchorWeekIsBig = true;

    // 星期勾选
    [ObservableProperty] private bool _mondayChecked = true;
    [ObservableProperty] private bool _tuesdayChecked = true;
    [ObservableProperty] private bool _wednesdayChecked = true;
    [ObservableProperty] private bool _thursdayChecked = true;
    [ObservableProperty] private bool _fridayChecked = true;
    [ObservableProperty] private bool _saturdayChecked;
    [ObservableProperty] private bool _sundayChecked;

    // ========== 摸鱼 ==========

    /// <summary>是否启用摸鱼历史</summary>
    [ObservableProperty]
    private bool _muyuHistoryEnabled = true;

    /// <summary>摸鱼按钮列表</summary>
    public ObservableCollection<MuyuButtonSettings> MuyuButtons { get; } = new();

    /// <summary>新按钮文字</summary>
    [ObservableProperty]
    private string _newMuyuLabel = "";

    /// <summary>新按钮 Emoji</summary>
    [ObservableProperty]
    private string _newMuyuEmoji = "";

    // ========== 政策参数 ==========

    [ObservableProperty] private decimal _pensionRatePercent = 8m;
    [ObservableProperty] private decimal _pensionMin = 4775m;
    [ObservableProperty] private decimal _pensionMax = 27549m;

    [ObservableProperty] private decimal _medicalTier1RatePercent = 2m;
    [ObservableProperty] private decimal _medicalTier2RatePercent = 0.5m;
    [ObservableProperty] private decimal _medicalMin = 6727m;
    [ObservableProperty] private decimal _medicalMax = 33633m;

    [ObservableProperty] private decimal _unemploymentRatePercent = 0.2m;
    [ObservableProperty] private decimal _unemploymentMin = 2520m;
    [ObservableProperty] private decimal _unemploymentMax = 44265m;

    /// <summary>节假日列表</summary>
    public ObservableCollection<HolidayEntry> HolidayDates { get; } = new();

    /// <summary>补班日列表（周末也上班计薪）</summary>
    public ObservableCollection<HolidayEntry> MakeupWorkDays { get; } = new();

    /// <summary>新增节假日日期文本</summary>
    [ObservableProperty]
    private string _newHolidayDate = "";

    /// <summary>新增节假日名称</summary>
    [ObservableProperty]
    private string _newHolidayName = "";

    /// <summary>新增补班日日期文本</summary>
    [ObservableProperty]
    private string _newMakeupDate = "";

    /// <summary>新增补班日名称</summary>
    [ObservableProperty]
    private string _newMakeupName = "";

    // ========== 更新 ==========

    /// <summary>共享的更新状态（与主界面提示条同源）</summary>
    public UpdateViewModel Update { get; }

    /// <summary>是否检查新版本</summary>
    [ObservableProperty]
    private bool _updateCheckEnabled = true;

    /// <summary>版本清单地址</summary>
    [ObservableProperty]
    private string _updateCheckUrl = "";

    // ========== 关于 ==========

    /// <summary>启动时显示提示页</summary>
    [ObservableProperty]
    private bool _showSplashOnStartup = true;

    // ========== 状态 ==========

    /// <summary>错误提示文本</summary>
    [ObservableProperty]
    private string _errorText = "";

    /// <summary>是否显示错误</summary>
    [ObservableProperty]
    private bool _isErrorVisible;

    /// <summary>保存成功事件</summary>
    public event EventHandler? Saved;

    /// <summary>取消事件</summary>
    public event EventHandler? Cancelled;

    /// <summary>改动后需要重算"计算说明"的属性集合</summary>
    private static readonly HashSet<string> DetailAffectingProperties = new()
    {
        nameof(PensionInputModeIndex), nameof(PensionBase), nameof(PensionAmount),
        nameof(MedicalTierIndex), nameof(MedicalInputModeIndex), nameof(MedicalBase), nameof(MedicalAmount),
        nameof(UnemploymentInputModeIndex), nameof(UnemploymentBase), nameof(UnemploymentAmount),
        nameof(HousingFundModeIndex), nameof(HousingFundAmount), nameof(HousingFundBase),
        nameof(HousingFundRate), nameof(HousingFundRatePercent), nameof(HousingFundCap),
        nameof(PensionRatePercent), nameof(PensionMin), nameof(PensionMax),
        nameof(MedicalTier1RatePercent), nameof(MedicalTier2RatePercent), nameof(MedicalMin), nameof(MedicalMax),
        nameof(UnemploymentRatePercent), nameof(UnemploymentMin), nameof(UnemploymentMax),
        nameof(GrossSalary), nameof(IsGrossMode)
    };

    public SettingsViewModel(UpdateViewModel? updateViewModel = null)
    {
        Update = updateViewModel ?? new UpdateViewModel();
        _live = AppServices.Config.Settings;
        LoadFromSettings(_live);

        PropertyChanged += (_, e) =>
        {
            var name = e.PropertyName;

            if (name == nameof(IsGrossMode))
                OnPropertyChanged(nameof(IsNetMode));

            if (name != null && DetailAffectingProperties.Contains(name))
            {
                OnPropertyChanged(nameof(PensionDetail));
                OnPropertyChanged(nameof(MedicalDetail));
                OnPropertyChanged(nameof(UnemploymentDetail));
                OnPropertyChanged(nameof(HousingFundDetail));
            }
        };
    }

    /// <summary>
    /// 从设置加载到界面（深拷贝草稿）
    /// </summary>
    private void LoadFromSettings(AppSettings settings)
    {
        IsGrossMode = settings.Mode == SalaryMode.Gross;
        GrossSalary = settings.GrossSalary;
        NetSalary = settings.NetSalary;

        PensionInputModeIndex = (int)settings.PensionInputMode;
        PensionBase = settings.PensionBase;
        PensionAmount = settings.PensionAmount;

        MedicalTierIndex = (int)settings.MedicalTier;
        MedicalInputModeIndex = (int)settings.MedicalInputMode;
        MedicalBase = settings.MedicalBase;
        MedicalAmount = settings.MedicalAmount;

        UnemploymentInputModeIndex = (int)settings.UnemploymentInputMode;
        UnemploymentBase = settings.UnemploymentBase;
        UnemploymentAmount = settings.UnemploymentAmount;

        HousingFundModeIndex = (int)settings.HousingFundMode;
        HousingFundAmount = settings.HousingFundAmount;
        HousingFundBase = settings.HousingFundBase;
        HousingFundRate = settings.HousingFundRate;
        HousingFundCap = settings.HousingFundCap;

        OtherDeductions.Clear();
        foreach (var d in settings.OtherDeductions)
            OtherDeductions.Add(new OtherDeduction { Name = d.Name, Amount = d.Amount });

        CurrentMonth = settings.CurrentMonth;
        SpecialDeduction = settings.SpecialDeduction;

        WorkStartText = settings.Shift.WorkStart.ToString(@"hh\:mm");
        WorkEndText = settings.Shift.WorkEnd.ToString(@"hh\:mm");
        if (settings.Shift.LunchBreaks.Count > 0)
        {
            LunchStartText = settings.Shift.LunchBreaks[0].Start.ToString(@"hh\:mm");
            LunchEndText = settings.Shift.LunchBreaks[0].End.ToString(@"hh\:mm");
        }
        DailyPayHours = settings.Shift.DailyPayHours;
        AlternateWeekEnabled = settings.Shift.AlternateWeekEnabled;
        AnchorWeekIsBig = settings.Shift.AnchorWeekIsBig;

        var days = settings.Shift.WorkDays;
        MondayChecked = days.Contains(DayOfWeek.Monday);
        TuesdayChecked = days.Contains(DayOfWeek.Tuesday);
        WednesdayChecked = days.Contains(DayOfWeek.Wednesday);
        ThursdayChecked = days.Contains(DayOfWeek.Thursday);
        FridayChecked = days.Contains(DayOfWeek.Friday);
        SaturdayChecked = days.Contains(DayOfWeek.Saturday);
        SundayChecked = days.Contains(DayOfWeek.Sunday);

        MuyuHistoryEnabled = settings.MuyuHistoryEnabled;
        MuyuButtons.Clear();
        foreach (var b in settings.MuyuButtons)
            MuyuButtons.Add(new MuyuButtonSettings { Id = b.Id, Label = b.Label, Emoji = b.Emoji });

        var ss = settings.SocialSecurity;
        PensionRatePercent = ss.PensionRate * 100;
        PensionMin = ss.PensionMin;
        PensionMax = ss.PensionMax;
        MedicalTier1RatePercent = ss.MedicalTier1Rate * 100;
        MedicalTier2RatePercent = ss.MedicalTier2Rate * 100;
        MedicalMin = ss.MedicalMin;
        MedicalMax = ss.MedicalMax;
        UnemploymentRatePercent = ss.UnemploymentRate * 100;
        UnemploymentMin = ss.UnemploymentMin;
        UnemploymentMax = ss.UnemploymentMax;

        HolidayDates.Clear();
        foreach (var h in settings.HolidayDates)
            HolidayDates.Add(new HolidayEntry { Date = h.Date, Name = h.Name });

        MakeupWorkDays.Clear();
        foreach (var m in settings.MakeupWorkDays)
            MakeupWorkDays.Add(new HolidayEntry { Date = m.Date, Name = m.Name });

        ShowSplashOnStartup = settings.ShowSplashOnStartup;

        UpdateCheckEnabled = settings.UpdateCheckEnabled;
        UpdateCheckUrl = settings.UpdateCheckUrl;
    }

    /// <summary>
    /// 校验输入
    /// </summary>
    private bool Validate(out string error)
    {
        if (!TimeSpan.TryParse(WorkStartText, out var start))
        {
            error = "上班时间格式不正确，应为 HH:mm（如 09:00）";
            return false;
        }
        if (!TimeSpan.TryParse(WorkEndText, out var end))
        {
            error = "下班时间格式不正确，应为 HH:mm（如 18:00）";
            return false;
        }
        if (end <= start)
        {
            error = "下班时间必须晚于上班时间";
            return false;
        }
        if (DailyPayHours <= 0)
        {
            error = "日计薪小时必须大于 0";
            return false;
        }
        if (CurrentMonth < 1 || CurrentMonth > 12)
        {
            error = "当前月份必须在 1-12 之间";
            return false;
        }
        if (IsGrossMode && GrossSalary <= 0)
        {
            error = "税前工资必须大于 0";
            return false;
        }
        if (!IsGrossMode && NetSalary <= 0)
        {
            error = "税后工资必须大于 0";
            return false;
        }

        error = "";
        return true;
    }

    /// <summary>
    /// 构建草稿设置
    /// </summary>
    private AppSettings BuildDraft()
    {
        var draft = DeepCopy(_live);

        draft.Mode = IsGrossMode ? SalaryMode.Gross : SalaryMode.Net;
        draft.GrossSalary = GrossSalary;
        draft.NetSalary = NetSalary;

        draft.PensionInputMode = (InsuranceInputMode)PensionInputModeIndex;
        draft.PensionBase = PensionBase;
        draft.PensionAmount = PensionAmount;

        draft.MedicalTier = (MedicalTier)MedicalTierIndex;
        draft.MedicalInputMode = (InsuranceInputMode)MedicalInputModeIndex;
        draft.MedicalBase = MedicalBase;
        draft.MedicalAmount = MedicalAmount;

        draft.UnemploymentInputMode = (InsuranceInputMode)UnemploymentInputModeIndex;
        draft.UnemploymentBase = UnemploymentBase;
        draft.UnemploymentAmount = UnemploymentAmount;

        draft.HousingFundMode = (HousingFundMode)HousingFundModeIndex;
        draft.HousingFundAmount = HousingFundAmount;
        draft.HousingFundBase = HousingFundBase;
        draft.HousingFundRate = HousingFundRate;
        draft.HousingFundCap = HousingFundCap;

        draft.OtherDeductions = new();
        foreach (var d in OtherDeductions)
            draft.OtherDeductions.Add(new OtherDeduction { Name = d.Name, Amount = d.Amount });

        draft.CurrentMonth = CurrentMonth;
        draft.SpecialDeduction = SpecialDeduction;

        // 班制
        TimeSpan.TryParse(WorkStartText, out var start);
        TimeSpan.TryParse(WorkEndText, out var end);
        TimeSpan.TryParse(LunchStartText, out var lunchStart);
        TimeSpan.TryParse(LunchEndText, out var lunchEnd);

        draft.Shift.WorkStart = start;
        draft.Shift.WorkEnd = end;
        draft.Shift.LunchBreaks = new()
        {
            new LunchBreak { Start = lunchStart, End = lunchEnd }
        };
        draft.Shift.DailyPayHours = DailyPayHours;
        draft.Shift.AlternateWeekEnabled = AlternateWeekEnabled;
        draft.Shift.AnchorWeekIsBig = AnchorWeekIsBig;

        var days = new System.Collections.Generic.List<DayOfWeek>();
        if (MondayChecked) days.Add(DayOfWeek.Monday);
        if (TuesdayChecked) days.Add(DayOfWeek.Tuesday);
        if (WednesdayChecked) days.Add(DayOfWeek.Wednesday);
        if (ThursdayChecked) days.Add(DayOfWeek.Thursday);
        if (FridayChecked) days.Add(DayOfWeek.Friday);
        if (SaturdayChecked) days.Add(DayOfWeek.Saturday);
        if (SundayChecked) days.Add(DayOfWeek.Sunday);
        draft.Shift.WorkDays = days;

        // 摸鱼
        draft.MuyuHistoryEnabled = MuyuHistoryEnabled;
        draft.MuyuButtons = new();
        foreach (var b in MuyuButtons)
            draft.MuyuButtons.Add(new MuyuButtonSettings { Id = b.Id, Label = b.Label, Emoji = b.Emoji });

        // 政策参数
        draft.SocialSecurity.PensionRate = PensionRatePercent / 100;
        draft.SocialSecurity.PensionMin = PensionMin;
        draft.SocialSecurity.PensionMax = PensionMax;
        draft.SocialSecurity.MedicalTier1Rate = MedicalTier1RatePercent / 100;
        draft.SocialSecurity.MedicalTier2Rate = MedicalTier2RatePercent / 100;
        draft.SocialSecurity.MedicalMin = MedicalMin;
        draft.SocialSecurity.MedicalMax = MedicalMax;
        draft.SocialSecurity.UnemploymentRate = UnemploymentRatePercent / 100;
        draft.SocialSecurity.UnemploymentMin = UnemploymentMin;
        draft.SocialSecurity.UnemploymentMax = UnemploymentMax;

        draft.HolidayDates = new();
        foreach (var h in HolidayDates)
            draft.HolidayDates.Add(new HolidayEntry { Date = h.Date, Name = h.Name });

        draft.MakeupWorkDays = new();
        foreach (var m in MakeupWorkDays)
            draft.MakeupWorkDays.Add(new HolidayEntry { Date = m.Date, Name = m.Name });

        draft.ShowSplashOnStartup = ShowSplashOnStartup;

        // 更新设置（这些字段不参与"计时将重置"判断）
        draft.UpdateCheckEnabled = UpdateCheckEnabled;
        draft.UpdateCheckUrl = UpdateCheckUrl.Trim();

        return draft;
    }

    /// <summary>
    /// 深拷贝（JSON 往返）
    /// </summary>
    private static AppSettings DeepCopy(AppSettings source)
    {
        var json = JsonSerializer.Serialize(source, JsonFileStore<AppSettings>.CreateDefaultOptions());
        return JsonSerializer.Deserialize<AppSettings>(json, JsonFileStore<AppSettings>.CreateDefaultOptions())!;
    }

    /// <summary>
    /// 判断是否修改了影响计算的字段
    /// </summary>
    private static bool AffectsCalculation(AppSettings a, AppSettings b)
    {
        return a.Mode != b.Mode
            || a.GrossSalary != b.GrossSalary
            || a.NetSalary != b.NetSalary
            || a.PensionInputMode != b.PensionInputMode || a.PensionBase != b.PensionBase || a.PensionAmount != b.PensionAmount
            || a.MedicalTier != b.MedicalTier || a.MedicalInputMode != b.MedicalInputMode
            || a.MedicalBase != b.MedicalBase || a.MedicalAmount != b.MedicalAmount
            || a.UnemploymentInputMode != b.UnemploymentInputMode
            || a.UnemploymentBase != b.UnemploymentBase || a.UnemploymentAmount != b.UnemploymentAmount
            || a.HousingFundMode != b.HousingFundMode || a.HousingFundAmount != b.HousingFundAmount
            || a.HousingFundBase != b.HousingFundBase || a.HousingFundRate != b.HousingFundRate
            || a.HousingFundCap != b.HousingFundCap
            || a.CurrentMonth != b.CurrentMonth || a.SpecialDeduction != b.SpecialDeduction
            || a.Shift.WorkStart != b.Shift.WorkStart || a.Shift.WorkEnd != b.Shift.WorkEnd
            || a.Shift.DailyPayHours != b.Shift.DailyPayHours
            || a.SocialSecurity.PensionRate != b.SocialSecurity.PensionRate
            || a.SocialSecurity.PensionMin != b.SocialSecurity.PensionMin
            || a.SocialSecurity.PensionMax != b.SocialSecurity.PensionMax
            || a.HolidayDates.Count != b.HolidayDates.Count
            || a.OtherDeductions.Count != b.OtherDeductions.Count;
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        IsErrorVisible = false;

        if (!Validate(out var error))
        {
            ErrorText = error;
            IsErrorVisible = true;
            return;
        }

        var draft = BuildDraft();

        // 判断是否需要确认
        if (AffectsCalculation(_live, draft))
        {
            var owner = Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0]
                : null;

            var confirmed = ConfirmDialog.Show(owner,
                "修改后当前计时将重置，已累计的今日/当月数据会重新计算，确定吗？",
                "确认修改");

            if (!confirmed) return;

            // 暂停 → 保存 → 失效缓存 → 恢复
            AppServices.Ticker.Pause();
            AppServices.Config.SaveSettings(draft);
            AppServices.Engine.InvalidateCache();
            AppServices.Ticker.Resume();
            AppServices.Ticker.RefreshNow();
        }
        else
        {
            AppServices.Config.SaveSettings(draft);
        }

        _live = AppServices.Config.Settings;
        Saved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 取消
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 恢复默认参数
    /// </summary>
    [RelayCommand]
    private void RestoreDefaults()
    {
        var owner = Application.Current?.Windows.Count > 0
            ? Application.Current.Windows[0]
            : null;

        var confirmed = ConfirmDialog.Show(owner,
            "确定要恢复所有政策参数为默认值吗？",
            "恢复默认参数");

        if (!confirmed) return;

        var defaults = SettingsDefaults.Create();
        LoadFromSettings(defaults);
    }

    /// <summary>
    /// 添加摸鱼按钮
    /// </summary>
    [RelayCommand]
    private void AddMuyuButton()
    {
        if (string.IsNullOrWhiteSpace(NewMuyuLabel)) return;

        MuyuButtons.Add(new MuyuButtonSettings
        {
            Id = Guid.NewGuid().ToString(),
            Label = NewMuyuLabel.Trim(),
            Emoji = string.IsNullOrWhiteSpace(NewMuyuEmoji) ? "🐟" : NewMuyuEmoji.Trim()
        });

        NewMuyuLabel = "";
        NewMuyuEmoji = "";
    }

    /// <summary>
    /// 删除摸鱼按钮
    /// </summary>
    [RelayCommand]
    private void RemoveMuyuButton(MuyuButtonSettings? button)
    {
        if (button != null) MuyuButtons.Remove(button);
    }

    /// <summary>
    /// 添加其他扣款
    /// </summary>
    [RelayCommand]
    private void AddOtherDeduction()
    {
        OtherDeductions.Add(new OtherDeduction { Name = "新扣款", Amount = 0 });
    }

    /// <summary>
    /// 删除其他扣款
    /// </summary>
    [RelayCommand]
    private void RemoveOtherDeduction(OtherDeduction? deduction)
    {
        if (deduction != null) OtherDeductions.Remove(deduction);
    }

    /// <summary>
    /// 添加节假日
    /// </summary>
    [RelayCommand]
    private void AddHoliday()
    {
        if (!DateOnly.TryParse(NewHolidayDate, out var date))
        {
            ErrorText = "节假日日期格式不正确，应为 yyyy-MM-dd（如 2026-10-01）";
            IsErrorVisible = true;
            return;
        }

        HolidayDates.Add(new HolidayEntry
        {
            Date = date,
            Name = string.IsNullOrWhiteSpace(NewHolidayName) ? "节假日" : NewHolidayName.Trim()
        });

        NewHolidayDate = "";
        NewHolidayName = "";
        IsErrorVisible = false;
    }

    /// <summary>
    /// 删除节假日
    /// </summary>
    [RelayCommand]
    private void RemoveHoliday(HolidayEntry? holiday)
    {
        if (holiday != null) HolidayDates.Remove(holiday);
    }

    /// <summary>
    /// 添加补班日
    /// </summary>
    [RelayCommand]
    private void AddMakeupDay()
    {
        if (!DateOnly.TryParse(NewMakeupDate, out var date))
        {
            ErrorText = "补班日日期格式不正确，应为 yyyy-MM-dd（如 2026-01-04）";
            IsErrorVisible = true;
            return;
        }

        MakeupWorkDays.Add(new HolidayEntry
        {
            Date = date,
            Name = string.IsNullOrWhiteSpace(NewMakeupName) ? "补班" : NewMakeupName.Trim()
        });

        NewMakeupDate = "";
        NewMakeupName = "";
        IsErrorVisible = false;
    }

    /// <summary>
    /// 删除补班日
    /// </summary>
    [RelayCommand]
    private void RemoveMakeupDay(HolidayEntry? entry)
    {
        if (entry != null) MakeupWorkDays.Remove(entry);
    }

    /// <summary>
    /// 立即检查更新（使用草稿中的地址，无需先保存）
    /// </summary>
    [RelayCommand]
    private async Task CheckUpdateNowAsync()
    {
        await Update.CheckForUpdateAsync(force: true, urlOverride: UpdateCheckUrl);
    }

    /// <summary>
    /// 应用班制预设
    /// </summary>
    [RelayCommand]
    private void ApplyShiftPreset()
    {
        var preset = ShiftPresetIndex switch
        {
            0 => ShiftPreset.FiveDayEightHour,
            1 => ShiftPreset.AlternatingWeek,
            2 => ShiftPreset.SixDay,
            3 => ShiftPreset.TenHourDay,
            _ => ShiftPreset.FiveDayEightHour
        };

        var shift = SettingsDefaults.Preset(preset);
        WorkStartText = shift.WorkStart.ToString(@"hh\:mm");
        WorkEndText = shift.WorkEnd.ToString(@"hh\:mm");
        if (shift.LunchBreaks.Count > 0)
        {
            LunchStartText = shift.LunchBreaks[0].Start.ToString(@"hh\:mm");
            LunchEndText = shift.LunchBreaks[0].End.ToString(@"hh\:mm");
        }
        DailyPayHours = shift.DailyPayHours;
        AlternateWeekEnabled = shift.AlternateWeekEnabled;

        MondayChecked = shift.WorkDays.Contains(DayOfWeek.Monday);
        TuesdayChecked = shift.WorkDays.Contains(DayOfWeek.Tuesday);
        WednesdayChecked = shift.WorkDays.Contains(DayOfWeek.Wednesday);
        ThursdayChecked = shift.WorkDays.Contains(DayOfWeek.Thursday);
        FridayChecked = shift.WorkDays.Contains(DayOfWeek.Friday);
        SaturdayChecked = shift.WorkDays.Contains(DayOfWeek.Saturday);
        SundayChecked = shift.WorkDays.Contains(DayOfWeek.Sunday);
    }
}
