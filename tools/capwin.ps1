# capwin.ps1 - Capture the largest visible window of a process via PrintWindow.
# Usage: powershell -File tools\capwin.ps1 -Path out.png [-ProcessId 1234]
param(
    [string]$Path = "window.png",
    [int]$ProcessId = 0
)

Add-Type -AssemblyName System.Drawing

$signature = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

public class WinCap
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    public static IntPtr FindLargest(uint targetPid)
    {
        IntPtr best = IntPtr.Zero;
        long bestArea = 0;
        EnumWindows((hWnd, lParam) =>
        {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid != targetPid || !IsWindowVisible(hWnd)) return true;

            RECT rect;
            if (!GetWindowRect(hWnd, out rect)) return true;
            long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
            if (area > bestArea) { bestArea = area; best = hWnd; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    public static string Describe(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetWindowText(hWnd, sb, sb.Capacity);
        RECT rect;
        GetWindowRect(hWnd, out rect);
        return sb.ToString() + " | " + (rect.Right - rect.Left) + "x" + (rect.Bottom - rect.Top) + " @ " + rect.Left + "," + rect.Top;
    }

    public static string Capture(IntPtr hWnd, string path)
    {
        RECT rect;
        if (!GetWindowRect(hWnd, out rect)) return "GetWindowRect failed";

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return "invalid window size: " + width + "x" + height;

        using (Bitmap bmp = new Bitmap(width, height))
        {
            using (Graphics gfx = Graphics.FromImage(bmp))
            {
                IntPtr hdc = gfx.GetHdc();
                bool ok = PrintWindow(hWnd, hdc, 2);
                gfx.ReleaseHdc(hdc);

                // Layered/transparent windows often return false; fall back to screen pixels.
                if (!ok)
                    gfx.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
            }
            bmp.Save(path, ImageFormat.Png);
        }
        return "OK " + width + "x" + height;
    }
}
"@

Add-Type -TypeDefinition $signature -Language CSharp -ReferencedAssemblies System.Drawing
[WinCap]::SetProcessDPIAware() | Out-Null

if ($ProcessId -gt 0) {
    $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
} else {
    $p = Get-Process -Name "SalaryTicker" -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (-not $p) {
    Write-Output "no SalaryTicker process found"
    exit 1
}

$hWnd = [WinCap]::FindLargest([uint32]$p.Id)
if ($hWnd -eq [IntPtr]::Zero) {
    Write-Output "no visible window found"
    exit 1
}

Write-Output ("capturing: " + [WinCap]::Describe($hWnd))
[WinCap]::ShowWindow($hWnd, 9) | Out-Null
[WinCap]::SetForegroundWindow($hWnd) | Out-Null
Start-Sleep -Milliseconds 900

$result = [WinCap]::Capture($hWnd, $Path)
Write-Output ("result: " + $result)
