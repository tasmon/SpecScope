using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace SpecScope
{
    /// <summary>
    /// Renders the notification-area icon: a hinted value on top and its unit underneath
    /// (%, °C, °F, B/s, KB/s, MB/s, GB/s), flat and monochrome like built-in Windows tray icons.
    /// </summary>
    public static class TrayIcon
    {
        /// <summary>Splits a byte rate into a short number and unit for the icon.</summary>
        public static void NetParts(double bps, out string value, out string unit)
        {
            if (bps < 1000) { value = bps < 1 ? "0" : bps.ToString("0"); unit = "B/s"; }
            else if (bps < 1000 * 1024.0) { value = (bps / 1024).ToString("0"); unit = "KB/s"; }
            else if (bps < 1000 * 1048576.0) { double m = bps / 1048576; value = m < 10 ? m.ToString("0.0") : m.ToString("0"); unit = "MB/s"; }
            else { double gb = bps / 1073741824.0; value = gb < 10 ? gb.ToString("0.0") : gb.ToString("0"); unit = "GB/s"; }
        }

        /// <summary>Draws one line of text centred horizontally in [x0, x0+w] and with its cap-height centred in the row. Shrinks to fit.</summary>
        static void Row(Graphics g, string text, float px, Color col, float x0, float w, float rowTop, float rowH)
        {
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            using (var br = new SolidBrush(col))
            {
                float size = px;
                Font f = new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Pixel);
                while (size > 5 && g.MeasureString(text, f, 10000, sf).Width > w)
                { f.Dispose(); size -= 0.5f; f = new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Pixel); }
                using (f)
                {
                    float asc = f.FontFamily.GetCellAscent(f.Style) / (float)f.FontFamily.GetEmHeight(f.Style) * size;
                    float baseline = rowTop + rowH / 2f + size * 0.70f / 2f;
                    float tw = g.MeasureString(text, f, 10000, sf).Width;
                    g.DrawString(text, f, br, x0 + (w - tw) / 2f, baseline - asc, sf);
                }
            }
        }

        /// <param name="arrow">0 none, 1 download (down arrow), 2 upload (up arrow)</param>
        public static Bitmap Render(int sz, string value, string unit, int arrow, Color valueColor, Color unitColor)
        {
            var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                float vh = (float)Math.Round(sz * 0.62f);          // value row height
                float vpx = value.Length <= 2 ? sz * 0.64f : (value.Length == 3 ? sz * 0.52f : sz * 0.44f);
                float upx = sz * 0.40f;
                float arrowW = arrow > 0 ? (float)Math.Round(Math.Max(3, sz * 0.2f)) : 0;

                Row(g, value, vpx, valueColor, arrowW, sz - arrowW, 0, vh);
                Row(g, unit, upx, unitColor, 0, sz, vh, sz - vh);
                if (arrow > 0)
                {
                    float ah = (float)Math.Round(Math.Max(4, sz * 0.26f)), ay = (vh - ah) / 2f, ax = 0.5f;
                    var tri = arrow == 1
                        ? new[] { new PointF(ax, ay), new PointF(ax + arrowW - 1, ay), new PointF(ax + (arrowW - 1) / 2f, ay + ah) }
                        : new[] { new PointF(ax, ay + ah), new PointF(ax + arrowW - 1, ay + ah), new PointF(ax + (arrowW - 1) / 2f, ay) };
                    using (var ab = new SolidBrush(valueColor)) g.FillPolygon(ab, tri);
                }
            }
            return bmp;
        }

        public static Icon ToIcon(Bitmap bmp)
        {
            IntPtr hi = bmp.GetHicon();
            try { using (var tmp = Icon.FromHandle(hi)) return (Icon)tmp.Clone(); }
            finally { DestroyIcon(hi); }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr h);
    }
}
