using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpecScope
{
    /// <summary>The four live gauges shared by the tray flyout and the desktop widget.</summary>
    static class LivePanel
    {
        public static void Draw(Graphics g, RectangleF a, float s, LiveMonitor mon)
        {
            float gap = 8 * s, cw = (a.Width - gap) / 2, ch = (a.Height - gap) / 2;
            double fr = mon.Frac;
            Tile(g, new RectangleF(a.X, a.Y, cw, ch), "CPU LOAD", mon.Cpu.ToString("0") + "%", 28, "Overall utilisation",
                mon.CpuH, 100, Theme.Level(mon.Cpu, Theme.Series(0)), null, Color.Empty, s, fr);
            bool nt = double.IsNaN(mon.TempC);
            Tile(g, new RectangleF(a.X + cw + gap, a.Y, cw, ch), "CPU TEMP", nt ? "N/A" : LiveMonitor.Temp(mon.TempC), 28,
                nt ? "Not exposed by hardware" : "Hottest thermal zone", mon.TempH, 100,
                nt ? Theme.Faint : (mon.TempC >= 80 ? Theme.Bad : mon.TempC >= 65 ? Theme.Warn : Theme.Series(2)), null, Color.Empty, s, fr);
            Tile(g, new RectangleF(a.X, a.Y + ch + gap, cw, ch), "RAM", mon.RamPct.ToString("0") + "%", 28,
                (mon.RamUsed / 1073741824.0).ToString("0.0") + " / " + (mon.RamTotal / 1073741824.0).ToString("0.0") + " GB",
                mon.RamH, 100, Theme.Level(mon.RamPct, Theme.Series(1)), null, Color.Empty, s, fr);
            Tile(g, new RectangleF(a.X + cw + gap, a.Y + ch + gap, cw, ch), "NETWORK", "\u2193 " + LiveMonitor.Rate(mon.Down), 17,
                "\u2191 " + LiveMonitor.Rate(mon.Up), mon.DownH, Theme.AutoMax(mon.DownH, mon.UpH, 100 * 1024), Theme.Series(0), mon.UpH, Theme.Series(3), s, fr);
        }

        static void Tile(Graphics g, RectangleF r, string title, string big, float bigPx, string small, List<double> hist, double max,
            Color accent, List<double> hist2, Color accent2, float s, double frac)
        {
            Theme.Card(g, r, 14 * s);
            float w = r.Width - 24 * s;
            Theme.Text(g, title, Theme.F(11 * s, true), Theme.Muted, new RectangleF(r.X + 14 * s, r.Y + 10 * s, w, 16 * s), TF.Line);
            Theme.Text(g, big, Theme.F(bigPx * s, true), Theme.Ink, new RectangleF(r.X + 14 * s, r.Y + 26 * s, w, (bigPx + 10) * s), TF.Line);
            Theme.Text(g, small, Theme.F(11.5f * s, false), Theme.Muted, new RectangleF(r.X + 14 * s, r.Y + 26 * s + (bigPx + 8) * s, w, 16 * s), TF.Line);
            var gr = new RectangleF(r.X + 12 * s, r.Bottom - 34 * s, r.Width - 24 * s, 24 * s);
            Theme.Spark(g, gr, hist, max, accent, frac);
            if (hist2 != null) Theme.Spark(g, gr, hist2, max, accent2, frac);
        }
    }

    /// <summary>Small popup with the four live gauges; opens from the tray icon.</summary>
    class Flyout : Form
    {
        readonly TrayApp app; readonly LiveMonitor mon;
        readonly Timer anim = new Timer { Interval = 50 };
        RectangleF openBtn; public int LastHide;

        public Flyout(TrayApp a, LiveMonitor m)
        {
            app = a; mon = m;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual; BackColor = Theme.Bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            float s; using (var g = CreateGraphics()) s = g.DpiX / 96f;
            ClientSize = new Size((int)(344 * s), (int)(322 * s));
            Deactivate += (o, e) => { LastHide = Environment.TickCount; Hide(); };
            VisibleChanged += (o, e) => { if (Visible) anim.Start(); else anim.Stop(); };
            anim.Tick += (o, e) => Invalidate();
            KeyPreview = true;
            KeyDown += (o, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
        }

        public void ShowNearTray()
        {
            float s; using (var g = CreateGraphics()) s = g.DpiX / 96f;
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - (int)(12 * s), wa.Bottom - Height - (int)(12 * s));
            using (var p = Theme.Round(new RectangleF(0, 0, Width, Height), Theme.Rad(20 * s))) Region = new Region(p);
            BackColor = Theme.Bg;
            Show(); Activate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        { if (openBtn.Contains(e.Location)) { Hide(); app.ShowMain(); } base.OnMouseDown(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; float s = g.DpiX / 96f;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.Rad(20 * s)))
            using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
            Theme.Text(g, "Live monitor", Theme.F(19 * s, true), Theme.Ink, new RectangleF(20 * s, 14 * s, 200 * s, 30 * s), TF.Line);
            openBtn = new RectangleF(Width - 92 * s, 16 * s, 72 * s, 30 * s);
            Theme.Pill(g, openBtn, "Open", false, s);
            LivePanel.Draw(g, new RectangleF(16 * s, 58 * s, Width - 32 * s, Height - 58 * s - 16 * s), s, mon);
        }

        protected override void Dispose(bool d) { if (d) anim.Dispose(); base.Dispose(d); }
    }

    /// <summary>Always-visible desktop widget: borderless, draggable, remembers its position.</summary>
    class Widget : Form
    {
        const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        readonly TrayApp app; readonly LiveMonitor mon;
        readonly Timer anim = new Timer { Interval = 50 };
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem miTop; readonly List<ToolStripMenuItem> opItems = new List<ToolStripMenuItem>();
        RectangleF closeBtn; bool dragging; Point dragFrom;

        public Widget(TrayApp a, LiveMonitor m)
        {
            app = a; mon = m;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            float s; using (var g = CreateGraphics()) s = g.DpiX / 96f;
            ClientSize = new Size((int)(344 * s), (int)(292 * s));
            anim.Tick += (o, e) => { if (Visible) Invalidate(); };
            BuildMenu();
            ContextMenuStrip = menu;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        { get { var cp = base.CreateParams; cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE; return cp; } }

        void BuildMenu()
        {
            menu.Renderer = new ToolStripProfessionalRenderer(new ThemeColors());
            menu.Font = new Font("Segoe UI", 9.5f); menu.ShowImageMargin = false;
            menu.Items.Add(Item("Open SpecScope", (s, e) => app.ShowMain()));
            miTop = Item("Keep on top", (s, e) => { Settings.WidgetOnTop = !Settings.WidgetOnTop; app.ApplySettings(); });
            menu.Items.Add(miTop);
            var op = new ToolStripMenuItem("Opacity");
            foreach (int v in new[] { 100, 85, 70 })
            {
                int val = v;
                var it = Item(val + " %", (s, e) => { Settings.WidgetOpacity = val; app.ApplySettings(); });
                it.Tag = val; opItems.Add(it); op.DropDownItems.Add(it);
            }
            op.DropDown.Renderer = menu.Renderer;
            menu.Items.Add(op);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item("Hide widget", (s, e) => { Settings.ShowWidget = false; app.ApplySettings(); }));
            menu.Opening += (s, e) =>
            {
                miTop.Checked = Settings.WidgetOnTop;
                foreach (var it in opItems) it.Checked = (int)it.Tag == Settings.WidgetOpacity;
                ApplyMenuTheme(menu.Items);
            };
        }

        static ToolStripMenuItem Item(string text, EventHandler click)
        { var i = new ToolStripMenuItem(text); i.Click += click; return i; }

        static void ApplyMenuTheme(ToolStripItemCollection items)
        {
            foreach (ToolStripItem i in items)
            {
                i.ForeColor = Theme.Ink;
                var m = i as ToolStripMenuItem;
                if (m != null && m.HasDropDownItems) { m.DropDown.BackColor = Theme.Surface; ApplyMenuTheme(m.DropDownItems); }
            }
        }

        /// <summary>Re-applies size-independent options (always-on-top, opacity, theme-dependent shape).</summary>
        public void ApplyOptions()
        {
            TopMost = Settings.WidgetOnTop;
            Opacity = Settings.WidgetOpacity / 100.0;
            BackColor = Theme.Bg; menu.BackColor = Theme.Surface;
            float s; using (var g = CreateGraphics()) s = g.DpiX / 96f;
            using (var p = Theme.Round(new RectangleF(0, 0, Width, Height), Theme.Rad(18 * s))) Region = new Region(p);
            Invalidate();
        }

        public void ShowWidget()
        {
            if (!Visible)
            {
                var pos = DefaultPos();
                Location = pos;
                ApplyOptions();
                Show();
                Native.RoundCorners(Handle);
                anim.Start();
            }
            else ApplyOptions();
        }

        public void HideWidget() { anim.Stop(); Hide(); }

        Point DefaultPos()
        {
            if (Settings.HasWidgetPos)
            {
                var want = new Rectangle(Settings.WidgetX, Settings.WidgetY, Width, Height);
                foreach (var sc in Screen.AllScreens)
                {
                    var inter = Rectangle.Intersect(sc.WorkingArea, want);
                    if (inter.Width >= Width / 2 && inter.Height >= Height / 2) return want.Location;   // still on a visible screen
                }
            }
            var wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Right - Width - 24, wa.Top + 24);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (closeBtn.Contains(e.Location)) { Settings.ShowWidget = false; app.ApplySettings(); return; }
                dragging = true; dragFrom = e.Location; Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) Location = new Point(Left + e.X - dragFrom.X, Top + e.Y - dragFrom.Y);
            else Cursor = closeBtn.Contains(e.Location) ? Cursors.Hand : Cursors.SizeAll;
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging) { dragging = false; Capture = false; Settings.SetWidgetPos(Left, Top); }
            base.OnMouseUp(e);
        }

        protected override void OnDoubleClick(EventArgs e) { app.ShowMain(); base.OnDoubleClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; float s = g.DpiX / 96f;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.Rad(18 * s)))
            using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
            Theme.Text(g, "Live monitor", Theme.F(13 * s, true), Theme.Muted, new RectangleF(16 * s, 8 * s, 200 * s, 22 * s), TF.Line);
            closeBtn = new RectangleF(Width - 36 * s, 6 * s, 26 * s, 26 * s);
            Theme.Text(g, "\u2715", Theme.Sym(12 * s), Theme.Faint, closeBtn, TF.Center);
            LivePanel.Draw(g, new RectangleF(12 * s, 40 * s, Width - 24 * s, Height - 40 * s - 12 * s), s, mon);
        }

        protected override void Dispose(bool d) { if (d) { anim.Dispose(); menu.Dispose(); } base.Dispose(d); }
    }
}
