using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpecScope
{
    /// <summary>GDI text-format presets. PreserveGraphicsClipping is essential: scrolled content must be clipped.</summary>
    static class TF
    {
        const TextFormatFlags B = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping;
        public const TextFormatFlags Wrap = B | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak;
        public const TextFormatFlags Line = B | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags Center = B | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        public const TextFormatFlags Right = B | TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
    }

    /// <summary>One flat colour theme. Text colours are opaque on purpose: GDI ClearType text ignores alpha.</summary>
    class ThemeDef
    {
        public string Key, Name;
        public bool Dark;
        public Color Bg, Surface, SurfaceAlt, Border, Hover, Ink, Muted, Faint, Dim, Accent, OnAccent, Good, Warn, Bad;
        public Color[] Series;   // 0 cpu/primary, 1 memory, 2 disk/temp, 3 secondary (upload/write), 4 gpu
        public Color[] Pal;      // sidebar icon tiles
        public float Radius = 1f; // corner-radius multiplier
    }

    /// <summary>Flat, solid-colour themes (no gradients) and the shared drawing helpers that use them.</summary>
    static class Theme
    {
        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        static readonly ThemeDef[] defs =
        {
            new ThemeDef { Key = "dark", Name = "Dark", Dark = true,
                Bg = C(18, 18, 20), Surface = C(30, 30, 34), SurfaceAlt = C(44, 44, 50), Border = C(58, 58, 66), Hover = C(54, 54, 62),
                Ink = C(245, 245, 247), Muted = C(172, 172, 182), Faint = C(122, 122, 132), Dim = C(92, 92, 100),
                Accent = C(10, 132, 255), OnAccent = Color.White, Good = C(48, 209, 88), Warn = C(255, 159, 10), Bad = C(255, 69, 58),
                Series = new[] { C(10, 132, 255), C(191, 90, 242), C(64, 200, 224), C(48, 209, 88), C(255, 55, 95) },
                Pal = new[] { C(10, 132, 255), C(94, 92, 230), C(255, 159, 10), C(48, 209, 88), C(191, 90, 242), C(255, 55, 95), C(64, 200, 224), C(255, 69, 58) } },
            new ThemeDef { Key = "light", Name = "Light", Dark = false,
                Bg = C(240, 240, 244), Surface = C(255, 255, 255), SurfaceAlt = C(236, 236, 242), Border = C(214, 214, 224), Hover = C(228, 228, 237),
                Ink = C(28, 28, 30), Muted = C(98, 98, 110), Faint = C(142, 142, 154), Dim = C(178, 178, 188),
                Accent = C(0, 122, 255), OnAccent = Color.White, Good = C(30, 160, 70), Warn = C(224, 134, 0), Bad = C(214, 48, 40),
                Series = new[] { C(0, 122, 255), C(150, 70, 210), C(0, 150, 180), C(30, 160, 70), C(230, 40, 80) },
                Pal = new[] { C(0, 122, 255), C(88, 86, 214), C(255, 149, 0), C(52, 199, 89), C(175, 82, 222), C(255, 45, 85), C(50, 173, 230), C(255, 59, 48) } },
            new ThemeDef { Key = "cyber", Name = "Cyberpunk", Dark = true, Radius = 0.3f,
                Bg = C(9, 7, 20), Surface = C(20, 15, 38), SurfaceAlt = C(34, 24, 62), Border = C(110, 40, 150), Hover = C(48, 30, 86),
                Ink = C(236, 240, 255), Muted = C(172, 162, 216), Faint = C(122, 110, 172), Dim = C(86, 76, 122),
                Accent = C(5, 217, 232), OnAccent = C(9, 7, 20), Good = C(57, 255, 20), Warn = C(249, 240, 2), Bad = C(255, 42, 109),
                Series = new[] { C(5, 217, 232), C(255, 42, 109), C(249, 240, 2), C(176, 70, 255), C(57, 255, 20) },
                Pal = new[] { C(5, 217, 232), C(255, 42, 109), C(249, 240, 2), C(176, 70, 255), C(57, 255, 20), C(255, 128, 0), C(60, 120, 255), C(255, 0, 200) } },
            new ThemeDef { Key = "intel", Name = "Intel", Dark = false, Radius = 0.7f,
                Bg = C(231, 238, 247), Surface = C(255, 255, 255), SurfaceAlt = C(220, 231, 244), Border = C(196, 211, 232), Hover = C(210, 223, 241),
                Ink = C(10, 28, 58), Muted = C(70, 90, 124), Faint = C(120, 138, 168), Dim = C(166, 178, 200),
                Accent = C(0, 113, 197), OnAccent = Color.White, Good = C(0, 150, 100), Warn = C(232, 134, 0), Bad = C(208, 40, 52),
                Series = new[] { C(0, 113, 197), C(0, 174, 239), C(0, 70, 140), C(80, 150, 214), C(0, 130, 170) },
                Pal = new[] { C(0, 113, 197), C(0, 174, 239), C(0, 70, 140), C(60, 140, 220), C(0, 150, 190), C(90, 110, 160), C(0, 100, 170), C(40, 90, 160) } },
            new ThemeDef { Key = "amd", Name = "AMD", Dark = true, Radius = 0.5f,
                Bg = C(13, 13, 14), Surface = C(26, 26, 28), SurfaceAlt = C(40, 40, 43), Border = C(64, 64, 68), Hover = C(52, 52, 56),
                Ink = C(246, 246, 246), Muted = C(172, 172, 176), Faint = C(122, 122, 126), Dim = C(88, 88, 92),
                Accent = C(237, 28, 36), OnAccent = Color.White, Good = C(70, 200, 110), Warn = C(255, 165, 0), Bad = C(255, 105, 80),
                Series = new[] { C(237, 28, 36), C(196, 196, 202), C(255, 120, 40), C(130, 130, 138), C(255, 80, 96) },
                Pal = new[] { C(237, 28, 36), C(196, 196, 202), C(255, 120, 40), C(150, 150, 158), C(200, 24, 32), C(255, 80, 96), C(120, 120, 128), C(255, 165, 0) } },
        };
        static ThemeDef T = defs[0];

        public static IList<ThemeDef> All { get { return defs; } }
        public static void Apply(string key)
        {
            T = defs[0];
            foreach (var d in defs) if (d.Key == key) { T = d; break; }
        }

        public static Color Bg { get { return T.Bg; } }
        public static Color Surface { get { return T.Surface; } }
        public static Color SurfaceAlt { get { return T.SurfaceAlt; } }
        public static Color Border { get { return T.Border; } }
        public static Color Hover { get { return T.Hover; } }
        public static Color Ink { get { return T.Ink; } }
        public static Color Muted { get { return T.Muted; } }
        public static Color Faint { get { return T.Faint; } }
        public static Color Dim { get { return T.Dim; } }
        public static Color Accent { get { return T.Accent; } }
        public static Color OnAccent { get { return T.OnAccent; } }
        public static Color Good { get { return T.Good; } }
        public static Color Warn { get { return T.Warn; } }
        public static Color Bad { get { return T.Bad; } }
        public static bool Dark { get { return T.Dark; } }
        public static Color Series(int i) { return T.Series[i % T.Series.Length]; }
        public static Color Pal(int i) { return T.Pal[i % T.Pal.Length]; }
        public static float Rad(float r) { return Math.Max(2f, r * T.Radius); }
        public static Color OnColor(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 150 ? Color.FromArgb(14, 14, 18) : Color.White; }
        public static Color Level(double p, Color baseColor) { return p >= 85 ? T.Bad : p >= 60 ? T.Warn : baseColor; }

        // ---- fonts
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static string family;

        static string Pick()
        {
            foreach (var n in new[] { "Segoe UI Variable Display", "Segoe UI" })
            { try { using (new FontFamily(n)) return n; } catch { } }
            return "Microsoft Sans Serif";
        }

        public static Font F(float px, bool bold)
        {
            if (family == null) family = Pick();
            string k = px.ToString("0.0") + (bold ? "b" : "r");
            Font f;
            if (!fonts.TryGetValue(k, out f))
            { f = new Font(family, px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); fonts[k] = f; }
            return f;
        }

        public static Font Sym(float px)
        {
            string k = "sym" + px.ToString("0.0"); Font f;
            if (!fonts.TryGetValue(k, out f)) { f = new Font("Segoe UI Symbol", px, FontStyle.Regular, GraphicsUnit.Pixel); fonts[k] = f; }
            return f;
        }

        // ---- text
        public static void Text(Graphics g, string s, Font f, Color c, RectangleF r, TextFormatFlags fl)
        {
            if (string.IsNullOrEmpty(s)) return;
            TextRenderer.DrawText(g, s, f, new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height)), c, fl);
        }

        public static Size Measure(Graphics g, string s, Font f, int w, TextFormatFlags fl)
        { return TextRenderer.MeasureText(g, string.IsNullOrEmpty(s) ? " " : s, f, new Size(Math.Max(1, w), 100000), fl); }

        // ---- shapes (all flat: solid fills + 1px borders on the pixel grid)
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Card(Graphics g, RectangleF r, float radius)
        {
            if (r.Height < 2 || r.Width < 2) return;
            float rad = Rad(radius);
            using (var p = Round(r, rad)) using (var b = new SolidBrush(T.Surface)) g.FillPath(b, p);
            using (var bp = Round(RectangleF.Inflate(r, -0.5f, -0.5f), rad)) using (var pen = new Pen(T.Border, 1f)) g.DrawPath(pen, bp);
        }

        public static void Pill(Graphics g, RectangleF r, string text, bool hot, float s)
        {
            float rad = Math.Min(r.Height / 2, Rad(r.Height / 2));
            using (var p = Round(r, rad)) using (var b = new SolidBrush(hot ? T.Hover : T.SurfaceAlt)) g.FillPath(b, p);
            using (var bp = Round(RectangleF.Inflate(r, -0.5f, -0.5f), rad)) using (var pen = new Pen(T.Border, 1f)) g.DrawPath(pen, bp);
            Text(g, text, F(13 * s, false), T.Ink, r, TF.Center);
        }

        /// <summary>Selectable chip. swatch (alpha &gt; 0) draws a small colour dot before the label.</summary>
        public static void Chip(Graphics g, RectangleF r, string text, bool sel, float s, Color swatch)
        {
            float rad = Math.Min(r.Height / 2, Rad(r.Height / 2));
            using (var p = Round(r, rad)) using (var b = new SolidBrush(sel ? T.Accent : T.SurfaceAlt)) g.FillPath(b, p);
            using (var bp = Round(RectangleF.Inflate(r, -0.5f, -0.5f), rad)) using (var pen = new Pen(sel ? T.Accent : T.Border, 1f)) g.DrawPath(pen, bp);
            var tr = r;
            if (swatch.A > 0)
            {
                float d = 10 * s;
                var dot = new RectangleF(r.X + 12 * s, r.Y + (r.Height - d) / 2, d, d);
                using (var b = new SolidBrush(swatch)) g.FillEllipse(b, dot);
                using (var pen = new Pen(sel ? T.OnAccent : T.Border, 1f)) g.DrawEllipse(pen, dot.X + .5f, dot.Y + .5f, dot.Width - 1, dot.Height - 1);
                tr = new RectangleF(r.X + 10 * s, r.Y, r.Width - 10 * s, r.Height);
            }
            Text(g, text, F(13 * s, sel), sel ? T.OnAccent : T.Ink, tr, TF.Center);
        }

        public static void Switch(Graphics g, RectangleF r, bool on, bool enabled)
        {
            Color off = T.Dark ? Color.FromArgb(80, 80, 90) : Color.FromArgb(196, 196, 206);
            Color track = on ? T.Accent : off;
            if (!enabled) track = Blend(track, T.Surface, 0.55f);
            using (var p = Round(r, r.Height / 2)) using (var b = new SolidBrush(track)) g.FillPath(b, p);
            float k = r.Height - 6, kx = on ? r.Right - k - 3 : r.X + 3;
            using (var kb = new SolidBrush(enabled ? Color.White : Blend(Color.White, T.Surface, 0.45f))) g.FillEllipse(kb, kx, r.Y + 3, k, k);
        }

        public static Color Blend(Color a, Color b, float t)
        { return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }

        public static void Tile(Graphics g, RectangleF r, Color c, string glyph)
        {
            using (var p = Round(r, Math.Max(3f, r.Height * 0.26f * T.Radius))) using (var b = new SolidBrush(c)) g.FillPath(b, p);
            Text(g, glyph, Sym(r.Height * 0.52f), OnColor(c), r, TF.Center);
        }

        public static double AutoMax(IList<double> a, IList<double> b, double floor)
        {
            double m = floor;
            foreach (var x in a) if (x * 1.15 > m) m = x * 1.15;
            if (b != null) foreach (var x in b) if (x * 1.15 > m) m = x * 1.15;
            return m;
        }

        /// <summary>
        /// Smoothly scrolling area graph (flat fill, no gradient). frac (0..1) is how far we are into the current
        /// sampling interval, so the graph glides left between samples instead of jumping once per tick.
        /// </summary>
        public static void Spark(Graphics g, RectangleF r, IList<double> v, double max, Color c, double frac)
        {
            int n = v.Count;
            if (n < 2 || r.Width < 4 || r.Height < 4 || max <= 0) return;
            float step = r.Width / (LiveMonitor.N - 1);
            var pts = new List<PointF>();
            for (int i = 0; i < n - 1; i++)
                pts.Add(new PointF(r.Right - (float)(n - 2 + frac - i) * step, Yv(r, v[i], max)));
            pts.Add(new PointF(r.Right, Yv(r, v[n - 2] + (v[n - 1] - v[n - 2]) * frac, max)));
            var area = new List<PointF>(pts);
            area.Add(new PointF(r.Right, r.Bottom)); area.Add(new PointF(pts[0].X, r.Bottom));
            var st = g.Save();
            g.SetClip(r, CombineMode.Intersect);
            using (var fb = new SolidBrush(Color.FromArgb(T.Dark ? 52 : 44, c))) g.FillPolygon(fb, area.ToArray());
            using (var pen = new Pen(c, 2f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts.ToArray());
            g.Restore(st);
        }

        static float Yv(RectangleF r, double val, double max)
        { return (float)(r.Bottom - Math.Min(1, Math.Max(0, val / max)) * (r.Height - 2) - 1); }
    }

    /// <summary>Context-menu colours that follow the active theme.</summary>
    class ThemeColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Hover; } }
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
        public override Color CheckBackground { get { return Theme.Hover; } }
        public override Color CheckSelectedBackground { get { return Theme.Hover; } }
        public override Color CheckPressedBackground { get { return Theme.Hover; } }
    }
}
