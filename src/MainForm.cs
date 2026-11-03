using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SpecScope
{
    class Hit { public RectangleF R; public Action A; }

    class Gauge
    {
        public string Title, Big, Sub, LegA, LegB;
        public Color C = Theme.Accent, C2 = Theme.Accent;
        public double Max = 100;
        public List<double> A, B;
    }

    /// <summary>Single custom-drawn surface: frosted sidebar + live gauges + scrolling glass cards + settings.</summary>
    class GlassView : Control
    {
        public List<Section> Data = new List<Section>();
        public int Sel;
        public bool ShowAdmin;
        public string WantSel;
        public event Action RefreshClicked, CopyClicked, ExportClicked, LiveClicked, AdminClicked, SettingsChanged;

        static readonly Dictionary<string, KeyValuePair<string, int>> icons = new Dictionary<string, KeyValuePair<string, int>>
        {
            { "Summary", new KeyValuePair<string, int>("\u2302", 0) },
            { "Operating System", new KeyValuePair<string, int>("\u229E", 1) },
            { "CPU", new KeyValuePair<string, int>("\u25A3", 2) },
            { "RAM", new KeyValuePair<string, int>("\u25A4", 3) },
            { "Motherboard", new KeyValuePair<string, int>("\u25C6", 4) },
            { "Graphics", new KeyValuePair<string, int>("\u25D0", 5) },
            { "Storage", new KeyValuePair<string, int>("\u25CF", 6) },
            { "Audio", new KeyValuePair<string, int>("\u266B", 7) },
            { "Network", new KeyValuePair<string, int>("\u2601", 0) },
            { "Peripherals", new KeyValuePair<string, int>("\u2328", 1) },
            { "Sensors & Battery", new KeyValuePair<string, int>("\u26A1", 2) },
            { "Settings", new KeyValuePair<string, int>("\u2699", -1) },
        };

        Graphics G; float S;
        float scroll, contentH, viewH;
        double frac;
        readonly List<KeyValuePair<RectangleF, int>> navRects = new List<KeyValuePair<RectangleF, int>>();
        readonly List<KeyValuePair<RectangleF, string>> btnRects = new List<KeyValuePair<RectangleF, string>>();
        readonly List<Hit> hits = new List<Hit>();
        readonly List<RectangleF> liveRects = new List<RectangleF>();
        int hoverNav = -1; string hoverBtn; bool drag; float dragOff;
        RectangleF thumb, view;
        string toast; readonly Timer toastTimer = new Timer { Interval = 1800 };
        readonly Timer anim = new Timer { Interval = 50 };

        public GlassView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true; BackColor = Theme.Bg;
            toastTimer.Tick += (s, e) => { toastTimer.Stop(); toast = null; Invalidate(); };
            anim.Tick += (s, e) => AnimTick();
            anim.Start();
        }

        public void Toast(string t) { toast = t; toastTimer.Stop(); toastTimer.Start(); Invalidate(); }
        public void LiveTick() { Invalidate(); }

        // Repaint just the graphs between samples so they glide smoothly without redrawing the whole window.
        void AnimTick()
        {
            if (!Visible || liveRects.Count == 0) return;
            var f = FindForm(); if (f == null || f.WindowState == FormWindowState.Minimized) return;
            foreach (var r in liveRects.ToArray()) Invalidate(Rectangle.Ceiling(r));
        }

        protected override bool IsInputKey(Keys k)
        { return k == Keys.Up || k == Keys.Down || k == Keys.PageUp || k == Keys.PageDown || k == Keys.Home || k == Keys.End || base.IsInputKey(k); }

        void Clamp() { scroll = Math.Min(Math.Max(scroll, 0), Math.Max(0, contentH - viewH)); }
        void Changed() { var h = SettingsChanged; if (h != null) h(); Invalidate(); }

        // =====================================================================  paint
        protected override void OnPaint(PaintEventArgs e)
        {
            G = e.Graphics; float s = S = G.DpiX / 96f;
            G.Clear(Theme.Bg);
            G.SmoothingMode = SmoothingMode.AntiAlias;
            navRects.Clear(); btnRects.Clear(); hits.Clear(); liveRects.Clear();
            if (WantSel != null)
            { int ix = Data.FindIndex(x => x.Name == WantSel); if (ix >= 0) { Sel = ix; scroll = 0; WantSel = null; } }

            float W = ClientSize.Width, H = ClientSize.Height, pad = 16 * s, sideW = 232 * s;
            PaintSidebar(pad, sideW, H);
            float cx = pad + sideW + pad, cw = W - cx - pad;
            var sec = (Sel >= 0 && Sel < Data.Count) ? Data[Sel] : null;
            bool special = sec != null && sec.Special;

            float reserved = (!special && sec != null) ? PaintButtons(W, pad) : 0;
            Theme.Text(G, sec != null ? sec.Name : AppInfo.Name, Theme.F(28 * s, true), Theme.Ink,
                new RectangleF(cx + 2 * s, pad, Math.Max(120 * s, cw - reserved - 8 * s), 44 * s), TF.Line);

            view = new RectangleF(cx, pad + 56 * s, cw, H - (pad + 56 * s) - pad);
            viewH = view.Height;
            var st = G.Save();
            G.SetClip(new Rectangle((int)view.X - 2, (int)view.Y, (int)view.Width + 4, (int)view.Height), CombineMode.Intersect);
            float y0 = view.Y - scroll, cy = y0;
            if (sec == null)
                Theme.Text(G, "Scanning your system\u2026", Theme.F(17 * s, false), Theme.Muted, view, TF.Center);
            else if (special) cy = DrawSettings(cx, cy, cw);
            else { cy = DrawLive(sec.Name, cx, cy, cw); cy = DrawCards(sec, cx, cy, cw); }
            contentH = Math.Max(0, cy - y0 - 14 * s);
            G.Restore(st);
            if (scroll > Math.Max(0, contentH - viewH)) { Clamp(); }

            if (contentH > viewH + 1)
            {
                float th = Math.Max(36 * s, viewH * viewH / contentH);
                float ty = view.Y + (viewH - th) * (scroll / (contentH - viewH));
                thumb = new RectangleF(W - 10 * s, ty, 5 * s, th);
                using (var p = Theme.Round(thumb, 2.5f * s))
                using (var b = new SolidBrush(Color.FromArgb(drag ? 200 : 130, Theme.Faint))) G.FillPath(b, p);
            }
            else thumb = RectangleF.Empty;

            if (toast != null)
            {
                float tw = Theme.Measure(G, toast, Theme.F(13 * s, false), 600, TF.Line).Width + 40 * s;
                Theme.Pill(G, new RectangleF(view.X + (view.Width - tw) / 2, H - pad - 48 * s, tw, 36 * s), toast, true, s);
            }
        }

        void PaintSidebar(float pad, float sideW, float H)
        {
            float s = S;
            var side = new RectangleF(pad, pad, sideW, H - 2 * pad);
            Theme.Card(G, side, 22 * s);
            Theme.Tile(G, new RectangleF(side.X + 16 * s, side.Y + 16 * s, 34 * s, 34 * s), Theme.Accent, "\u25CE");
            Theme.Text(G, AppInfo.Name, Theme.F(19 * s, true), Theme.Ink, new RectangleF(side.X + 60 * s, side.Y + 16 * s, 160 * s, 34 * s), TF.Line);
            float y = side.Y + 68 * s;
            for (int i = 0; i < Data.Count; i++)
            {
                if (Data[i].Special)
                {
                    y += 6 * s;
                    using (var pen = new Pen(Theme.Border)) G.DrawLine(pen, side.X + 16 * s, (float)Math.Floor(y) + .5f, side.Right - 16 * s, (float)Math.Floor(y) + .5f);
                    y += 8 * s;
                }
                var r = new RectangleF(side.X + 10 * s, y, sideW - 20 * s, 40 * s);
                navRects.Add(new KeyValuePair<RectangleF, int>(r, i));
                if (i == Sel || i == hoverNav)
                    using (var p = Theme.Round(r, Theme.Rad(11 * s)))
                    using (var b = new SolidBrush(i == Sel ? Theme.Blend(Theme.Surface, Theme.Accent, Theme.Dark ? 0.26f : 0.16f) : Theme.Hover)) G.FillPath(b, p);
                KeyValuePair<string, int> ic;
                if (!icons.TryGetValue(Data[i].Name, out ic)) ic = new KeyValuePair<string, int>("\u25CF", 0);
                Theme.Tile(G, new RectangleF(r.X + 8 * s, r.Y + 7 * s, 26 * s, 26 * s), ic.Value < 0 ? Theme.Faint : Theme.Pal(ic.Value), ic.Key);
                Theme.Text(G, Data[i].Name, Theme.F(14 * s, i == Sel), Theme.Ink, new RectangleF(r.X + 44 * s, r.Y, r.Width - 48 * s, r.Height), TF.Line);
                y += 43 * s;
            }
        }

        float PaintButtons(float W, float pad)
        {
            float s = S;
            var names = new List<string> { "Refresh", "Copy", "Export", Settings.ShowWidget ? "Hide Widget" : "Show Widget" };
            if (ShowAdmin) names.Add("Run as admin");
            float bx = W - pad, start = bx, bh = 34 * s;
            for (int i = names.Count - 1; i >= 0; i--)
            {
                float bw = Theme.Measure(G, names[i], Theme.F(13 * s, false), 600, TF.Line).Width + 30 * s;
                var r = new RectangleF(bx - bw, pad + 2 * s, bw, bh);
                btnRects.Add(new KeyValuePair<RectangleF, string>(r, names[i]));
                Theme.Pill(G, r, names[i], hoverBtn == names[i], s);
                bx -= bw + 8 * s;
            }
            return start - bx;
        }

        // =====================================================================  live gauges
        static string Gb(ulong b) { return (b / 1073741824.0).ToString("0.0") + " GB"; }

        static string Window()
        {
            int secs = (int)((LiveMonitor.N - 1) * LiveMonitor.I.IntervalMs / 1000.0);
            return secs >= 120 ? (secs / 60) + " minutes ago" : secs + " seconds ago";
        }

        Gauge GCpu(LiveMonitor m)
        {
            return new Gauge { Title = "CPU LOAD", Big = m.Cpu.ToString("0") + "%", C = Theme.Level(m.Cpu, Theme.Series(0)), A = m.CpuH,
                Sub = m.CpuMhz > 0 ? (m.CpuMhz / 1000).ToString("0.00") + " GHz  \u00B7  " + Environment.ProcessorCount + " threads" : Environment.ProcessorCount + " logical processors" };
        }
        Gauge GMem(LiveMonitor m)
        {
            return new Gauge { Title = "MEMORY", Big = m.RamPct.ToString("0") + "%", C = Theme.Level(m.RamPct, Theme.Series(1)), A = m.RamH, Sub = Gb(m.RamUsed) + " of " + Gb(m.RamTotal) + " in use" };
        }
        Gauge GDisk(LiveMonitor m)
        {
            return new Gauge { Title = "DISK ACTIVITY", Big = m.DiskActive.ToString("0") + "%", C = Theme.Series(2), A = m.DiskH, Sub = "Active time" };
        }
        Gauge GNet(LiveMonitor m)
        {
            return new Gauge { Title = "NETWORK", Big = "\u2193 " + LiveMonitor.Rate(m.Down), Sub = "\u2191 " + LiveMonitor.Rate(m.Up), A = m.DownH, B = m.UpH,
                C = Theme.Series(0), C2 = Theme.Series(3), Max = Theme.AutoMax(m.DownH, m.UpH, 100 * 1024) };
        }
        Gauge GDown(LiveMonitor m)
        {
            return new Gauge { Title = "DOWNLOAD", Big = LiveMonitor.Rate(m.Down), Sub = "Peak " + LiveMonitor.Rate(m.DownH.Count > 0 ? m.DownH.Max() : 0),
                A = m.DownH, C = Theme.Series(0), Max = Theme.AutoMax(m.DownH, null, 100 * 1024) };
        }
        Gauge GUp(LiveMonitor m)
        {
            return new Gauge { Title = "UPLOAD", Big = LiveMonitor.Rate(m.Up), Sub = "Peak " + LiveMonitor.Rate(m.UpH.Count > 0 ? m.UpH.Max() : 0),
                A = m.UpH, C = Theme.Series(3), Max = Theme.AutoMax(m.UpH, null, 100 * 1024) };
        }
        Gauge GDiskIo(LiveMonitor m)
        {
            return new Gauge { Title = "DISK THROUGHPUT", Big = LiveMonitor.Rate(m.DiskRead + m.DiskWrite), Sub = "Read " + LiveMonitor.Rate(m.DiskRead) + "  \u00B7  Write " + LiveMonitor.Rate(m.DiskWrite),
                A = m.DiskRH, B = m.DiskWH, C = Theme.Series(2), C2 = Theme.Series(3), LegA = "Read", LegB = "Write", Max = Theme.AutoMax(m.DiskRH, m.DiskWH, 1048576) };
        }
        Gauge GGpu(LiveMonitor m)
        {
            bool na = double.IsNaN(m.GpuLoad);
            return new Gauge { Title = "GPU LOAD", Big = na ? "N/A" : m.GpuLoad.ToString("0") + "%", C = na ? Theme.Faint : Theme.Level(m.GpuLoad, Theme.Series(4)), A = m.GpuH,
                Sub = na ? "Not reported by this GPU / driver" : "Busiest engine" };
        }
        Gauge GVram(LiveMonitor m)
        {
            bool na = double.IsNaN(m.GpuLoad) && m.GpuMem == 0;
            return new Gauge { Title = "GPU MEMORY", Big = na ? "N/A" : (m.GpuMem / 1073741824.0).ToString("0.00") + " GB", C = Theme.Series(1), A = m.GpuMH,
                Sub = "Dedicated memory in use", Max = Theme.AutoMax(m.GpuMH, null, 256.0 * 1048576) };
        }
        Gauge GTemp(LiveMonitor m)
        {
            bool na = double.IsNaN(m.TempC);
            return new Gauge { Title = "CPU TEMPERATURE", Big = na ? "N/A" : LiveMonitor.Temp(m.TempC), A = m.TempH,
                C = na ? Theme.Faint : (m.TempC >= 80 ? Theme.Bad : m.TempC >= 65 ? Theme.Warn : Theme.Series(2)),
                Sub = na ? "Not exposed by this hardware \u2013 try Run as admin" : "Hottest thermal zone" };
        }
        Gauge GBat(LiveMonitor m)
        {
            double p = double.IsNaN(m.BatteryPct) ? 0 : m.BatteryPct;
            return new Gauge { Title = "BATTERY", Big = p.ToString("0") + "%", A = m.BatH, C = p < 20 ? Theme.Bad : p < 40 ? Theme.Warn : Theme.Good,
                Sub = m.Charging ? "Plugged in" : "On battery" };
        }

        float DrawLive(string name, float cx, float y, float cw)
        {
            float s = S; var m = LiveMonitor.I; frac = m.Frac;
            var list = new List<Gauge>(); bool hero = true; int cols = 1;
            switch (name)
            {
                case "Summary": hero = false; list.Add(GCpu(m)); list.Add(GMem(m)); list.Add(GDisk(m)); list.Add(GNet(m)); cols = cw >= 760 * s ? 4 : 2; break;
                case "CPU": list.Add(GCpu(m)); break;
                case "RAM": list.Add(GMem(m)); break;
                case "Graphics": cols = 2; list.Add(GGpu(m)); list.Add(GVram(m)); break;
                case "Storage": cols = 2; list.Add(GDisk(m)); list.Add(GDiskIo(m)); break;
                case "Network": cols = 2; list.Add(GDown(m)); list.Add(GUp(m)); break;
                case "Sensors & Battery": list.Add(GTemp(m)); if (m.HasBattery) { list.Add(GBat(m)); cols = 2; } break;
                default: return y;
            }
            float gap = 14 * s, h = (hero ? 214 : 156) * s, w = (cw - (cols - 1) * gap) / cols;
            for (int i = 0; i < list.Count; i++)
                DrawGauge(new RectangleF(cx + (i % cols) * (w + gap), y + (i / cols) * (h + gap), w, h), list[i], hero);
            y += ((list.Count + cols - 1) / cols) * (h + gap);
            if (name == "CPU") y = DrawCores(cx, y, cw);
            return y;
        }

        void DrawGauge(RectangleF r, Gauge k, bool hero)
        {
            float s = S, ip = 18 * s;
            if (r.Bottom < view.Y - 4 || r.Y > view.Bottom + 4) return;
            Theme.Card(G, r, 18 * s);
            Theme.Text(G, k.Title, Theme.F(12 * s, true), Theme.Muted, new RectangleF(r.X + ip, r.Y + 14 * s, r.Width - 2 * ip, 18 * s), TF.Line);

            if (k.LegA != null)
            {
                float lx = r.Right - ip;
                foreach (var leg in new[] { new KeyValuePair<string, Color>(k.LegB, k.C2), new KeyValuePair<string, Color>(k.LegA, k.C) })
                {
                    float lw = Theme.Measure(G, leg.Key, Theme.F(12 * s, false), 300, TF.Line).Width;
                    Theme.Text(G, leg.Key, Theme.F(12 * s, false), Theme.Muted, new RectangleF(lx - lw, r.Y + 14 * s, lw + 2, 18 * s), TF.Line);
                    using (var b = new SolidBrush(leg.Value)) G.FillEllipse(b, lx - lw - 13 * s, r.Y + 20.5f * s, 7 * s, 7 * s);
                    lx -= lw + 24 * s;
                }
            }

            float availW = r.Width - 2 * ip, px = (hero ? 34 : 30) * s;
            while (px > 15 * s && Theme.Measure(G, k.Big, Theme.F(px, true), 2000, TF.Line).Width > availW) px -= 2 * s;
            Theme.Text(G, k.Big, Theme.F(px, true), Theme.Ink, new RectangleF(r.X + ip, r.Y + 32 * s, availW, 42 * s), TF.Line);
            Theme.Text(G, k.Sub, Theme.F(13 * s, false), Theme.Muted, new RectangleF(r.X + ip, r.Y + (hero ? 76 : 72) * s, availW, 20 * s), TF.Line);

            float gy = r.Y + (hero ? 108 : 100) * s, gh = r.Bottom - gy - (hero ? 32 : 14) * s;
            var gr = new RectangleF(r.X + ip, gy, availW, gh);
            using (var pen = new Pen(Theme.Border))
            {
                int lines = hero ? 3 : 1;
                for (int i = 1; i <= lines; i++)
                {
                    float ly = (float)Math.Floor(gr.Y + gr.Height * i / (lines + (hero ? 1 : 0))) + .5f;
                    if (!hero) ly = (float)Math.Floor(gr.Bottom) - .5f;
                    G.DrawLine(pen, gr.X, ly, gr.Right, ly);
                }
            }
            Theme.Spark(G, gr, k.A, k.Max, k.C, frac);
            if (k.B != null) Theme.Spark(G, gr, k.B, k.Max, k.C2, frac);
            if (hero)
            {
                Theme.Text(G, Window(), Theme.F(11.5f * s, false), Theme.Faint, new RectangleF(gr.X, gr.Bottom + 6 * s, 160 * s, 18 * s), TF.Line);
                Theme.Text(G, "now", Theme.F(11.5f * s, false), Theme.Faint, new RectangleF(gr.Right - 60 * s, gr.Bottom + 6 * s, 60 * s, 18 * s), TF.Right);
            }
            liveRects.Add(RectangleF.Inflate(gr, 3, 3));
        }

        float DrawCores(float cx, float y, float cw)
        {
            float s = S, ip = 18 * s; var cs = LiveMonitor.I.Cores;
            if (cs.Length == 0) return y;
            int cols = cw >= 700 * s ? 2 : 1, rows = (cs.Length + cols - 1) / cols;
            float rh = 30 * s, h = 44 * s + rows * rh + 6 * s, gap = 24 * s;
            var card = new RectangleF(cx, y, cw, h);
            if (card.Bottom > view.Y - 20 && card.Y < view.Bottom + 20)
            {
                Theme.Card(G, card, 18 * s);
                Theme.Text(G, "Logical processors", Theme.F(13 * s, true), Theme.Muted, new RectangleF(card.X + ip, card.Y + 11 * s, 300 * s, 22 * s), TF.Line);
                float cellW = (cw - 2 * ip - (cols - 1) * gap) / cols;
                for (int i = 0; i < cs.Length; i++)
                {
                    float x = card.X + ip + (i % cols) * (cellW + gap), ry = card.Y + 44 * s + (i / cols) * rh;
                    Theme.Text(G, "CPU " + i, Theme.F(13 * s, false), Theme.Muted, new RectangleF(x, ry, 54 * s, rh), TF.Line);
                    var bar = new RectangleF(x + 58 * s, ry + rh / 2 - 4 * s, cellW - 58 * s - 52 * s, 8 * s);
                    using (var p = Theme.Round(bar, 4 * s)) using (var b = new SolidBrush(Theme.SurfaceAlt)) G.FillPath(b, p);
                    float fw = (float)(bar.Width * cs[i] / 100.0);
                    if (fw >= 2 * s) using (var p = Theme.Round(new RectangleF(bar.X, bar.Y, fw, bar.Height), 4 * s)) using (var b = new SolidBrush(Theme.Level(cs[i], Theme.Series(0)))) G.FillPath(b, p);
                    Theme.Text(G, cs[i].ToString("0") + "%", Theme.F(13 * s, false), Theme.Ink, new RectangleF(x + cellW - 48 * s, ry, 48 * s, rh), TF.Right);
                }
            }
            return y + h + 14 * s;
        }

        // =====================================================================  static info cards
        float RowH(Row r, float valW)
        {
            if (r.HW != valW)
            { r.H = Math.Max(36 * S, Theme.Measure(G, r.Text, Theme.F(14 * S, false), (int)valW, TF.Wrap).Height + 16 * S); r.HW = valW; }
            return r.H;
        }

        float DrawCards(Section sec, float cx, float cy, float cw)
        {
            float s = S;
            if (sec.Rows.Count == 0)
            {
                if (cy < view.Y + 40 * s)
                    Theme.Text(G, "No information available", Theme.F(17 * s, false), Theme.Muted, new RectangleF(view.X, cy, view.Width, 60 * s), TF.Center);
                return cy;
            }
            var groups = new List<KeyValuePair<string, List<Row>>>();
            foreach (var r in sec.Rows)
            {
                if (groups.Count == 0 || groups[groups.Count - 1].Key != r.Group)
                    groups.Add(new KeyValuePair<string, List<Row>>(r.Group, new List<Row>()));
                groups[groups.Count - 1].Value.Add(r);
            }
            float ip = 18 * s, keyW = Math.Max(130 * s, (cw - 2 * ip) * 0.32f);
            float valX = ip + keyW + 14 * s, valW = cw - valX - ip;
            var kf = Theme.F(14 * s, false);
            foreach (var grp in groups)
            {
                float hh = grp.Key != null ? 36 * s : 6 * s;
                var hs = grp.Value.Select(r => RowH(r, valW)).ToList();
                float ch = hh + hs.Sum() + 6 * s;
                var card = new RectangleF(cx, cy, cw, ch);
                if (card.Bottom > view.Y - 20 && card.Y < view.Bottom + 20)
                {
                    Theme.Card(G, card, 18 * s);
                    float ry = card.Y;
                    if (grp.Key != null)
                        Theme.Text(G, grp.Key, Theme.F(13 * s, true), Theme.Muted, new RectangleF(card.X + ip, card.Y + 11 * s, card.Width - 2 * ip, 22 * s), TF.Line);
                    ry += hh;
                    for (int i = 0; i < grp.Value.Count; i++)
                    {
                        var r = grp.Value[i];
                        if (i > 0) using (var pen = new Pen(Theme.Border))
                                G.DrawLine(pen, card.X + ip, (float)Math.Floor(ry) + .5f, card.Right - ip, (float)Math.Floor(ry) + .5f);
                        Theme.Text(G, r.Key, kf, Theme.Ink, new RectangleF(card.X + ip, ry + 9 * s, keyW, hs[i] - 9 * s), TF.Wrap);
                        Theme.Text(G, r.Text, kf, r.Live != null ? Theme.Ink : Theme.Muted, new RectangleF(card.X + valX, ry + 9 * s, valW, hs[i] - 9 * s), TF.Wrap);
                        ry += hs[i];
                    }
                }
                cy += ch + 14 * s;
            }
            return cy;
        }

        // =====================================================================  settings page
        RectangleF BeginCard(string title, float cx, float y, float cw, float body)
        {
            float s = S;
            var card = new RectangleF(cx, y, cw, 36 * s + body + 8 * s);
            Theme.Card(G, card, 18 * s);
            Theme.Text(G, title, Theme.F(13 * s, true), Theme.Muted, new RectangleF(card.X + 18 * s, card.Y + 11 * s, card.Width - 36 * s, 22 * s), TF.Line);
            return card;
        }

        void Divider(RectangleF card, float y)
        {
            using (var pen = new Pen(Theme.Border))
                G.DrawLine(pen, card.X + 18 * S, (float)Math.Floor(y) + .5f, card.Right - 18 * S, (float)Math.Floor(y) + .5f);
        }

        void ToggleRow(RectangleF card, ref float ry, bool first, string title, string desc, bool on, bool enabled, Action click)
        {
            float s = S, ip = 18 * s, rh = 68 * s;
            if (!first) Divider(card, ry);
            Theme.Text(G, title, Theme.F(15 * s, false), enabled ? Theme.Ink : Theme.Dim, new RectangleF(card.X + ip, ry + 11 * s, card.Width - 2 * ip - 80 * s, 22 * s), TF.Line);
            Theme.Text(G, desc, Theme.F(12.5f * s, false), enabled ? Theme.Muted : Theme.Dim, new RectangleF(card.X + ip, ry + 35 * s, card.Width - 2 * ip - 80 * s, 20 * s), TF.Line);
            Theme.Switch(G, new RectangleF(card.Right - ip - 52 * s, ry + (rh - 30 * s) / 2, 52 * s, 30 * s), on, enabled);
            if (enabled) hits.Add(new Hit { R = new RectangleF(card.X, ry, card.Width, rh), A = click });
            ry += rh;
        }

        void ChipRow(RectangleF card, ref float ry, bool first, string title, string desc, string[] labels, int sel, Action<int> pick, Color[] swatches = null)
        {
            float s = S, ip = 18 * s, rh = 96 * s;
            if (!first) Divider(card, ry);
            Theme.Text(G, title, Theme.F(15 * s, false), Theme.Ink, new RectangleF(card.X + ip, ry + 11 * s, card.Width - 2 * ip, 22 * s), TF.Line);
            Theme.Text(G, desc, Theme.F(12.5f * s, false), Theme.Muted, new RectangleF(card.X + ip, ry + 35 * s, card.Width - 2 * ip, 20 * s), TF.Line);
            float x = card.X + ip;
            for (int i = 0; i < labels.Length; i++)
            {
                float w = Theme.Measure(G, labels[i], Theme.F(13 * s, true), 600, TF.Line).Width + 30 * s + (swatches != null ? 12 * s : 0);
                var r = new RectangleF(x, ry + 56 * s, w, 32 * s);
                Theme.Chip(G, r, labels[i], i == sel, s, swatches != null ? swatches[i] : Color.Empty);
                int idx = i; hits.Add(new Hit { R = r, A = () => pick(idx) });
                x += w + 8 * s;
            }
            ry += rh;
        }

        void InfoRow(RectangleF card, ref float ry, bool first, string k, string v)
        {
            float s = S, ip = 18 * s;
            if (!first) Divider(card, ry);
            Theme.Text(G, k, Theme.F(14 * s, false), Theme.Ink, new RectangleF(card.X + ip, ry, 160 * s, 40 * s), TF.Line);
            Theme.Text(G, v, Theme.F(14 * s, false), Theme.Muted, new RectangleF(card.X + ip + 180 * s, ry, card.Width - 2 * ip - 180 * s, 40 * s), TF.Line);
            ry += 40 * s;
        }

        float DrawSettings(float cx, float y, float cw)
        {
            float s = S, gap = 14 * s;
            bool tray = Settings.ShowTrayIcon, bg = Settings.CanRunInBackground;

            var c1 = BeginCard("General", cx, y, cw, 3 * 68 * s);
            float ry = c1.Y + 36 * s;
            ToggleRow(c1, ref ry, true, "Show notification-area icon", "Live readout beside the clock. Click it for the live monitor, right-click for options.",
                tray, true, () => { Settings.ShowTrayIcon = !Settings.ShowTrayIcon; Changed(); });
            ToggleRow(c1, ref ry, false, "Run in background",
                bg ? "Keep SpecScope running after you close the window." : "Needs the notification-area icon or the desktop widget \u2013 otherwise closing the window quits SpecScope.",
                bg && Settings.CloseToTray, bg, () => { Settings.CloseToTray = !Settings.CloseToTray; Changed(); });
            ToggleRow(c1, ref ry, false, "Start with Windows", "Launch when you sign in \u2013 quietly in the background if the icon or widget is on.",
                Settings.StartWithWindows, true, () => { Settings.StartWithWindows = !Settings.StartWithWindows; Changed(); });
            y = c1.Bottom + gap;

            var c2 = BeginCard("Desktop widget", cx, y, cw, 2 * 68 * s + 96 * s);
            ry = c2.Y + 36 * s;
            ToggleRow(c2, ref ry, true, "Show desktop widget", "A small always-visible live monitor. Drag it anywhere; right-click it for options.",
                Settings.ShowWidget, true, () => { Settings.ShowWidget = !Settings.ShowWidget; Changed(); });
            ToggleRow(c2, ref ry, false, "Keep widget on top", "Off lets other windows cover it.",
                Settings.WidgetOnTop, Settings.ShowWidget, () => { Settings.WidgetOnTop = !Settings.WidgetOnTop; Changed(); });
            int[] ops = { 100, 85, 70 };
            ChipRow(c2, ref ry, false, "Widget opacity", "Make the widget more transparent.",
                new[] { "100 %", "85 %", "70 %" }, Math.Max(0, Array.IndexOf(ops, Settings.WidgetOpacity)),
                i => { Settings.WidgetOpacity = ops[i]; Changed(); });
            y = c2.Bottom + gap;

            var c3 = BeginCard("Appearance", cx, y, cw, 96 * s);
            ry = c3.Y + 36 * s;
            var defs = Theme.All; var names = new string[defs.Count]; var sw = new Color[defs.Count]; int cur = 0;
            for (int i = 0; i < defs.Count; i++) { names[i] = defs[i].Name; sw[i] = defs[i].Accent; if (defs[i].Key == Settings.ThemeKey) cur = i; }
            ChipRow(c3, ref ry, true, "Theme", "Flat colour themes for the window, flyout and widget.", names, cur,
                i => { Settings.ThemeKey = defs[i].Key; Changed(); }, sw);
            y = c3.Bottom + gap;

            var c4 = BeginCard("Monitoring", cx, y, cw, 3 * 96 * s);
            ry = c4.Y + 36 * s;
            string[] mk = { "cpu", "temp", "ram", "down", "up" };
            ChipRow(c4, ref ry, true, "Notification-area icon shows", "The value on the tray icon, with its unit underneath.",
                new[] { "CPU load", "CPU temp", "RAM", "Download", "Upload" }, Math.Max(0, Array.IndexOf(mk, Settings.TrayMetric)),
                i => { Settings.TrayMetric = mk[i]; Changed(); });
            int[] iv = { 1000, 2000, 5000 };
            ChipRow(c4, ref ry, false, "Update interval", "How often live data is sampled. Longer = lighter on the CPU and a longer graph window.",
                new[] { "1 second", "2 seconds", "5 seconds" }, Math.Max(0, Array.IndexOf(iv, Settings.IntervalMs)),
                i => { Settings.IntervalMs = iv[i]; Changed(); });
            ChipRow(c4, ref ry, false, "Temperature unit", "Used for the tray icon, tooltip, flyout, widget and graphs.",
                new[] { "Celsius (\u00B0C)", "Fahrenheit (\u00B0F)" }, Settings.Fahrenheit ? 1 : 0,
                i => { Settings.Fahrenheit = i == 1; Changed(); });
            y = c4.Bottom + gap;

            var c5 = BeginCard("About", cx, y, cw, 4 * 40 * s);
            ry = c5.Y + 36 * s;
            InfoRow(c5, ref ry, true, "Version", AppInfo.Version);
            InfoRow(c5, ref ry, false, "Developer & publisher", AppInfo.Developer);
            InfoRow(c5, ref ry, false, "Contact", AppInfo.Email);
            InfoRow(c5, ref ry, false, "License", "MIT \u2013 open source");
            return c5.Bottom + gap;
        }

        // =====================================================================  input
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Focus(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (drag)
            {
                float th = thumb.Height, track = viewH - th;
                if (track > 0) { scroll = (e.Y - dragOff - view.Y) / track * (contentH - viewH); Clamp(); Invalidate(); }
                return;
            }
            int hn = -1; string hb = null; bool oh = false;
            foreach (var kv in navRects) if (kv.Key.Contains(e.Location)) hn = kv.Value;
            foreach (var kv in btnRects) if (kv.Key.Contains(e.Location)) hb = kv.Value;
            if (view.Contains(e.Location)) foreach (var h in hits) if (h.R.Contains(e.Location)) oh = true;
            Cursor = (hn >= 0 || hb != null || oh) ? Cursors.Hand : Cursors.Default;
            if (hn != hoverNav || hb != hoverBtn) { hoverNav = hn; hoverBtn = hb; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hoverNav = -1; hoverBtn = null; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Focus();
            if (!thumb.IsEmpty && RectangleF.Inflate(thumb, 8 * S, 0).Contains(e.Location))
            { drag = true; dragOff = e.Y - thumb.Y; Invalidate(); return; }
            foreach (var kv in navRects)
                if (kv.Key.Contains(e.Location)) { Sel = kv.Value; scroll = 0; Invalidate(); return; }
            foreach (var kv in btnRects)
                if (kv.Key.Contains(e.Location))
                {
                    Action a = null;
                    switch (kv.Value)
                    {
                        case "Refresh": a = RefreshClicked; break;
                        case "Copy": a = CopyClicked; break;
                        case "Export": a = ExportClicked; break;
                        case "Show Widget": case "Hide Widget": a = LiveClicked; break;
                        case "Run as admin": a = AdminClicked; break;
                    }
                    if (a != null) a();
                    return;
                }
            if (view.Contains(e.Location))
                foreach (var h in hits.ToArray()) if (h.R.Contains(e.Location)) { h.A(); return; }
        }

        protected override void OnMouseUp(MouseEventArgs e) { if (drag) { drag = false; Invalidate(); } }

        protected override void OnMouseWheel(MouseEventArgs e)
        { scroll -= e.Delta / 120f * 64 * S; Clamp(); Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            float page = viewH * 0.9f;
            switch (e.KeyCode)
            {
                case Keys.Down: if (Sel < Data.Count - 1) { Sel++; scroll = 0; } break;
                case Keys.Up: if (Sel > 0) { Sel--; scroll = 0; } break;
                case Keys.PageDown: scroll += page; break;
                case Keys.PageUp: scroll -= page; break;
                case Keys.Home: scroll = 0; break;
                case Keys.End: scroll = contentH; break;
                default: base.OnKeyDown(e); return;
            }
            Clamp(); Invalidate(); e.Handled = true;
        }

        protected override void Dispose(bool d) { if (d) { anim.Dispose(); toastTimer.Dispose(); } base.Dispose(d); }

        public void ApplyTheme() { BackColor = Theme.Bg; Invalidate(); }
    }

    class MainForm : Form
    {
        readonly TrayApp app;
        readonly GlassView view = new GlassView();

        public MainForm(TrayApp a)
        {
            app = a;
            Text = AppInfo.Name + " \u2013 System Information" + (AppInfo.IsAdmin() ? " (Administrator)" : "");
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BackColor = Theme.Bg; AutoScaleMode = AutoScaleMode.None;
            float s; using (var g = CreateGraphics()) s = g.DpiX / 96f;
            ClientSize = new Size((int)(1120 * s), (int)(740 * s));
            MinimumSize = new Size((int)(1000 * s), (int)(680 * s));
            StartPosition = FormStartPosition.CenterScreen;
            view.Dock = DockStyle.Fill; view.ShowAdmin = !AppInfo.IsAdmin();
            Controls.Add(view);
            view.RefreshClicked += Rescan;
            view.CopyClicked += CopySection;
            view.ExportClicked += Export;
            view.LiveClicked += app.ToggleWidget;
            view.AdminClicked += Elevate;
            view.SettingsChanged += app.ApplySettings;
            HandleCreated += (o, e) => { Native.Chrome(Handle, Theme.Bg, Theme.Ink, Theme.Dark); Native.RoundCorners(Handle); };
            Shown += (o, e) => Rescan();
        }

        public void LiveTick() { view.LiveTick(); }
        public void RepaintSettings() { view.Invalidate(); }
        public void ApplyTheme()
        {
            BackColor = Theme.Bg; view.ApplyTheme();
            if (IsHandleCreated) Native.Chrome(Handle, Theme.Bg, Theme.Ink, Theme.Dark);
        }
        public void GoToSettings() { view.WantSel = "Settings"; view.Invalidate(); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && Settings.CanRunInBackground && Settings.CloseToTray && !app.Exiting)
            { e.Cancel = true; Hide(); app.HiddenToTray(); }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        { base.OnFormClosed(e); if (!app.Exiting) app.ExitApp(); }

        void Rescan()
        {
            UseWaitCursor = true;
            string keep = view.Sel >= 0 && view.Sel < view.Data.Count ? view.Data[view.Sel].Name : null;
            Task.Factory.StartNew(() => Collector.All()).ContinueWith(t =>
            {
                UseWaitCursor = false;
                if (t.IsFaulted) { view.Toast("Scan failed: " + t.Exception.GetBaseException().Message); return; }
                var list = t.Result; list.Add(new Section("Settings") { Special = true });
                view.Data = list;
                int ix = keep == null ? 0 : list.FindIndex(x => x.Name == keep);
                view.Sel = ix < 0 ? 0 : ix;
                view.Invalidate();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        static string Render(Section sec)
        {
            var sb = new StringBuilder(); string last = "\0";
            sb.AppendLine("== " + sec.Name + " ==");
            foreach (var r in sec.Rows)
            {
                if (r.Group != last) { if (r.Group != null) sb.AppendLine("[" + r.Group + "]"); last = r.Group; }
                sb.AppendLine("  " + r.Key.PadRight(24) + r.Text);
            }
            return sb.ToString();
        }

        void CopySection()
        {
            if (view.Sel < view.Data.Count && !view.Data[view.Sel].Special) { Clipboard.SetText(Render(view.Data[view.Sel])); view.Toast("Copied to clipboard"); }
        }

        void Export()
        {
            using (var d = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = "SpecScope-" + Environment.MachineName + ".txt" })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("SpecScope " + AppInfo.Version + " report \u2013 " + Environment.MachineName + " \u2013 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm")).AppendLine();
                    foreach (var s in view.Data) if (!s.Special) sb.AppendLine(Render(s));
                    File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                    view.Toast("Report saved");
                }
        }

        void Elevate()
        {
            try { Process.Start(new ProcessStartInfo(Application.ExecutablePath, "/elevated") { Verb = "runas", UseShellExecute = true }); app.ExitApp(); }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
