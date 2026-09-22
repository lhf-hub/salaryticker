# click.ps1 —— 自动化测试辅助脚本：模拟鼠标点击与截图
# 用法：powershell -File tools\click.ps1 -Action click -X 963 -Y 727
#       powershell -File tools\click.ps1 -Action shot -Path shot.png
param(
    [string]$Action = "click",
    [int]$X = 0,
    [int]$Y = 0,
    [string]$Path = "screenshot.png",
    [int]$DelayMs = 300
)

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class MouseHelper {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, int e);
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(200);
        mouse_event(0x0002, 0, 0, 0, 0);
        System.Threading.Thread.Sleep(80);
        mouse_event(0x0004, 0, 0, 0, 0);
    }
}
'@

if ($Action -eq "click") {
    [MouseHelper]::Click($X, $Y)
    Write-Output "clicked ($X, $Y)"
}
elseif ($Action -eq "shot") {
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing
    $b = New-Object System.Drawing.Bitmap([System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width, [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.CopyFromScreen(0, 0, 0, 0, $b.Size)
    $b.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $b.Dispose()
    Write-Output "screenshot saved: $Path"
}
