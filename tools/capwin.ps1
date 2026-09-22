# capwin.ps1 - Capture a specific window's content via PrintWindow (works when desktop capture returns blank)
# Usage: powershell -File tools\capwin.ps1 -Title "*实时计算器*" -Path out.png
param(
    [string]$Title = "*SalaryTicker*",
    [string]$Path = "window.png"
)

Add-Type -AssemblyName System.Drawing

$signature = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public class WinCap
{
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out int left, out int top, out int right, out int bottom);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static string Capture(IntPtr hWnd, string path)
    {
        int left;
        int top;
        int right;
        int bottom;
        if (!GetWindowRect(hWnd, out left, out top, out right, out bottom))
            return "GetWindowRect failed";

        int width = right - left;
        int height = bottom - top;
        if (width <= 0 || height <= 0)
            return "invalid window size: " + width + "x" + height;

        using (Bitmap bmp = new Bitmap(width, height))
        {
            using (Graphics gfx = Graphics.FromImage(bmp))
            {
                IntPtr hdc = gfx.GetHdc();
                bool ok = PrintWindow(hWnd, hdc, 2);
                gfx.ReleaseHdc(hdc);
                if (!ok) return "PrintWindow returned false";
            }
            bmp.Save(path, ImageFormat.Png);
        }
        return "OK " + width + "x" + height;
    }
}
"@

Add-Type -TypeDefinition $signature -Language CSharp -ReferencedAssemblies System.Drawing

# Pick the SalaryTicker process (title matching with non-ASCII args is unreliable in PS 5.1)
$p = Get-Process -Name "SalaryTicker" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) {
    Write-Output "no SalaryTicker window found"
    exit 1
}

Write-Output ("capturing window: " + $p.Id)

[WinCap]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
[WinCap]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 900

$result = [WinCap]::Capture($p.MainWindowHandle, $Path)
Write-Output ("result: " + $result)
