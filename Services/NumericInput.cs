// ============================================================================
// 文件名：NumericInput.cs
// 功能描述：数值输入框的字符过滤纯逻辑（设置窗口的 PreviewTextInput / 粘贴都用它）
// ============================================================================

namespace SalaryTicker.Services;

/// <summary>
/// 数值输入过滤
/// </summary>
public static class NumericInput
{
    /// <summary>
    /// 判断这段新输入是否可接受：只允许数字与**一个小数点**。
    /// 输入框里根本敲不进字母，就不会出现"格式看着对、绑定却转换失败、
    /// 保存悄悄用回旧值"这类问题。
    /// </summary>
    /// <param name="currentText">输入框现有文本（用来判断是否已经有小数点）</param>
    /// <param name="incoming">即将插入的文本（一次按键可能是一个字符，粘贴则是一整段）</param>
    public static bool IsAcceptable(string? currentText, string? incoming)
    {
        if (string.IsNullOrEmpty(incoming)) return true;

        // 按字符范围比较，不用 char.IsDigit —— 后者会放行全角数字，
        // 而 decimal 的绑定转换只认半角
        var hasDot = (currentText ?? "").Contains('.');

        foreach (var c in incoming)
        {
            if (c >= '0' && c <= '9') continue;

            if (c != '.') return false;
            if (hasDot) return false;
            hasDot = true;
        }

        return true;
    }
}
