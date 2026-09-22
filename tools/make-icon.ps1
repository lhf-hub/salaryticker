# ============================================================================
# make-icon.ps1 - Generate the SalaryTicker application icon (.ico)
#
# Design: rounded square with coral gradient + white CNY symbol
#         (matches the app's warm-light design language)
# Output: Assets/SalaryTicker.ico (multi-size: 16/24/32/48/64/128/256)
#
# Usage: powershell -File tools/make-icon.ps1
# Note : ASCII-only source to avoid PowerShell 5.1 encoding issues.
# ============================================================================

Add-Type -AssemblyName System.Drawing

$code = @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public class IconMaker
{
    // Coral gradient (matches PrimaryBrush #E07A5F family)
    static readonly Color CoralTop    = Color.FromArgb(255, 0xF2, 0x8E, 0x72);
    static readonly Color CoralBottom = Color.FromArgb(255, 0xD9, 0x66, 0x4A);

    public static void Generate(string path)
    {
        // Hybrid layout for maximum compatibility:
        //   <= 64 px : BMP/DIB entries (readable by GDI+, old shell paths)
        //   >= 128 px: PNG entries (smaller file, Vista+ supported)
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var blobs = new List<byte[]>();
        foreach (int s in sizes)
            blobs.Add(s <= 64 ? RenderDib(s) : RenderPng(s));

        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using (var fs = File.Create(path))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((short)0);              // reserved
            bw.Write((short)1);              // type = icon
            bw.Write((short)sizes.Length);   // image count

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                bw.Write((byte)(s >= 256 ? 0 : s));   // width
                bw.Write((byte)(s >= 256 ? 0 : s));   // height
                bw.Write((byte)0);                     // palette
                bw.Write((byte)0);                     // reserved
                bw.Write((short)1);                    // planes
                bw.Write((short)32);                   // bpp
                bw.Write(blobs[i].Length);
                bw.Write(offset);
                offset += blobs[i].Length;
            }
            foreach (var b in blobs) bw.Write(b);
        }
    }

    /// <summary>Render one size as a BMP/DIB icon entry (BITMAPINFOHEADER + BGRA + AND mask).</summary>
    static byte[] RenderDib(int size)
    {
        using (var bmp = RenderBitmap(size))
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            int xorSize = size * size * 4;
            int andStride = ((size + 31) / 32) * 4;
            int andSize = andStride * size;

            // BITMAPINFOHEADER
            bw.Write(40);                 // biSize
            bw.Write(size);               // biWidth
            bw.Write(size * 2);           // biHeight (XOR + AND)
            bw.Write((short)1);           // biPlanes
            bw.Write((short)32);          // biBitCount
            bw.Write(0);                  // biCompression = BI_RGB
            bw.Write(xorSize + andSize);  // biSizeImage
            bw.Write(0); bw.Write(0);     // resolution
            bw.Write(0); bw.Write(0);     // colors

            // XOR bitmap: bottom-up BGRA
            for (int y = size - 1; y >= 0; y--)
                for (int x = 0; x < size; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                }

            // AND mask: all zero (alpha channel carries transparency)
            bw.Write(new byte[andSize]);

            return ms.ToArray();
        }
    }

    static byte[] RenderPng(int size)
    {
        using (var bmp = RenderBitmap(size))
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    /// <summary>Public helper for previewing the artwork at any size.</summary>
    public static void SavePreview(int size, string path)
    {
        using (var bmp = RenderBitmap(size))
            bmp.Save(path, ImageFormat.Png);
    }

    static Bitmap RenderBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float pad = size * 0.035f;
                float side = size - pad * 2f;
                float radius = size * 0.235f;

                // --- rounded square with coral gradient ---
                using (var path = RoundedRect(pad, pad, side, side, radius))
                using (var brush = new LinearGradientBrush(
                           new RectangleF(pad, pad, side, side), CoralTop, CoralBottom, 60f))
                {
                    g.FillPath(brush, path);
                }

                // --- CNY symbol (drawn as strokes for crispness at any size) ---
                float stroke = size * (size <= 24 ? 0.115f : 0.088f);
                using (var pen = new Pen(Color.White, stroke))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    // two diagonals meeting at the centre
                    g.DrawLine(pen, size * 0.305f, size * 0.205f, size * 0.500f, size * 0.435f);
                    g.DrawLine(pen, size * 0.695f, size * 0.205f, size * 0.500f, size * 0.435f);
                    // vertical stem
                    g.DrawLine(pen, size * 0.500f, size * 0.435f, size * 0.500f, size * 0.795f);

                    if (size > 20)
                    {
                        // two horizontal bars (skipped on tiny sizes to stay legible)
                        g.DrawLine(pen, size * 0.345f, size * 0.560f, size * 0.655f, size * 0.560f);
                        g.DrawLine(pen, size * 0.345f, size * 0.680f, size * 0.655f, size * 0.680f);
                    }
                }
            }

        return bmp;
    }

    static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        float d = r * 2f;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root "Assets\SalaryTicker.ico"

[IconMaker]::Generate($target)

# Also emit PNG previews (for visual review / README)
$previewBig = Join-Path $root "Assets\icon-preview-256.png"
$previewSmall = Join-Path $root "Assets\icon-preview-48.png"
[IconMaker]::SavePreview(256, $previewBig)
[IconMaker]::SavePreview(48, $previewSmall)

if (Test-Path $target) {
    $size = [math]::Round((Get-Item $target).Length / 1KB, 1)
    Write-Output "icon generated: $target ($size KB)"
    Write-Output "previews: $previewBig , $previewSmall"
} else {
    Write-Output "FAILED: icon not created"
}
