using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SpecScope
{
    /// <summary>Owns the notification-area icon, its menu, the live monitor, the desktop widget and the main window.</summary>
    class TrayApp : ApplicationContext
    {
        public bool Exiting;
        readonly NotifyIcon ni = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly LiveMonitor mon = LiveMonitor.I;
        readonly Timer timer = new Timer();
        readonly Control marshal = new Control();
        readonly Dictionary<string, ToolStripMenuItem> metricItems = new Dictionary<string, ToolStripMenuItem>();
        ToolStripMenuItem miCpu, miTemp, miRam, miNet, miStart, miClose, miWidget;
        MainForm main; Flyout fly; Widget widget; Icon curIcon; string lastKey, lastTheme; bool lightTaskbar; int themeCheck;
        readonly UserPreferenceChangedEventHandler prefHandler;

        public TrayApp(bool startHidden)
        {
            Theme.Apply(Settings.ThemeKey); lastTheme = Settings.ThemeKey;
            var h = marshal.Handle;
            lightTaskbar = LightTaskbar();
            prefHandler = (s, e) => { try { marshal.BeginInvoke(new Action(() => { lightTaskbar = LightTaskbar(); lastKey = null; Refresh(); })); } catch { } };
            SystemEvents.UserPreferenceChanged += prefHandler;
            BuildMenu();
            ni.ContextMenuStrip = menu;
            ni.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) ToggleFlyout(); };
            ni.DoubleClick += (s, e) => ShowMain();
            timer.Tick += (s, e) =>
            {
                mon.Tick(); Refresh();
                if (main != null && !main.IsDisposed && main.Visible) main.LiveTick();
                if (fly != null && fly.Visible) fly.Invalidate();
            };
            mon.Tick();
            timer.Start();
            if (!startHidden || !Settings.CanRunInBackground) ShowMain();
            ApplySettings();
        }

        /// <summary>Applies the user's settings to the live parts of the app (Settings page, tray menu, widget menu).</summary>
        public void ApplySettings()
        {
            if (Settings.ThemeKey != lastTheme)
            {
                lastTheme = Settings.ThemeKey; Theme.Apply(lastTheme);
                if (main != null && !main.IsDisposed) main.ApplyTheme();
                if (fly != null && !fly.IsDisposed) fly.Invalidate();
            }
            mon.IntervalMs = Settings.IntervalMs; timer.Interval = Settings.IntervalMs;
            lastKey = null; Refresh();
            ni.Visible = Settings.ShowTrayIcon;

            if (Settings.ShowWidget)
            {
                if (widget == null || widget.IsDisposed) widget = new Widget(this, mon);
                widget.ShowWidget();
            }
            else if (widget != null && !widget.IsDisposed) widget.HideWidget();

            if (main != null && !main.IsDisposed) main.RepaintSettings();
            // never leave the app running with nothing to click on
            if (!Settings.CanRunInBackground && (main == null || main.IsDisposed || !main.Visible)) ShowMain();
        }

        public void ToggleWidget() { Settings.ShowWidget = !Settings.ShowWidget; ApplySettings(); }

        // ---------- menu
        void BuildMenu()
        {
            menu.Renderer = new ToolStripProfessionalRenderer(new ThemeColors());
            menu.Font = new Font("Segoe UI", 9.5f); menu.ShowImageMargin = false;
            var open = Item("Open SpecScope", (s, e) => ShowMain()); open.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(open);
            menu.Items.Add(Item("Live monitor flyout", (s, e) => ToggleFlyout()));
            miWidget = Item("Desktop widget", (s, e) => ToggleWidget());
            menu.Items.Add(miWidget);
            menu.Items.Add(Item("Settings\u2026", (s, e) => { ShowMain(); main.GoToSettings(); }));
            menu.Items.Add(new ToolStripSeparator());
            miCpu = Info(); miTemp = Info(); miRam = Info(); miNet = Info();
            menu.Items.AddRange(new ToolStripItem[] { miCpu, miTemp, miRam, miNet });
            menu.Items.Add(new ToolStripSeparator());

            var show = new ToolStripMenuItem("Tray icon shows");
            string[,] opts = { { "cpu", "CPU load" }, { "temp", "CPU temperature" }, { "ram", "RAM usage" }, { "down", "Network download" }, { "up", "Network upload" } };
            for (int i = 0; i < opts.GetLength(0); i++)
            {
                string key = opts[i, 0];
                var it = Item(opts[i, 1], (s, e) => { Settings.TrayMetric = key; ApplySettings(); });
                metricItems[key] = it; show.DropDownItems.Add(it);
            }
            show.DropDown.Renderer = menu.Renderer;
            menu.Items.Add(show);
            miStart = Item("Start with Windows", (s, e) => { Settings.StartWithWindows = !Settings.StartWithWindows; ApplySettings(); });
            miClose = Item("Run in background", (s, e) => { Settings.CloseToTray = !Settings.CloseToTray; ApplySettings(); });
            menu.Items.Add(miStart); menu.Items.Add(miClose);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item("Exit", (s, e) => ExitApp()));
            menu.Opening += (s, e) =>
            {
                miStart.Checked = Settings.StartWithWindows; miClose.Checked = Settings.CloseToTray; miWidget.Checked = Settings.ShowWidget;
                foreach (var kv in metricItems) kv.Value.Checked = kv.Key == Settings.TrayMetric;
                UpdateInfo();
                menu.BackColor = Theme.Surface; ThemeItems(menu.Items);
            };
        }

        static void ThemeItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem i in items)
            {
                if (i.Enabled) i.ForeColor = Theme.Ink; else i.ForeColor = Theme.Muted;
                var m = i as ToolStripMenuItem;
                if (m != null && m.HasDropDownItems) { m.DropDown.BackColor = Theme.Surface; ThemeItems(m.DropDownItems); }
            }
        }

        ToolStripMenuItem Item(string text, EventHandler click)
        { var i = new ToolStripMenuItem(text); i.Click += click; return i; }

        static ToolStripMenuItem Info() { return new ToolStripMenuItem("\u2026") { Enabled = false }; }

        void UpdateInfo()
        {
            miCpu.Text = "CPU load:  " + mon.Cpu.ToString("0") + " %";
            miTemp.Text = "CPU temperature:  " + (double.IsNaN(mon.TempC) ? "not available" : LiveMonitor.Temp(mon.TempC));
            miRam.Text = "RAM:  " + Gb(mon.RamUsed) + " / " + Gb(mon.RamTotal) + "  (" + mon.RamPct.ToString("0") + " %)";
            miNet.Text = "Network:  \u2193 " + LiveMonitor.Rate(mon.Down) + "   \u2191 " + LiveMonitor.Rate(mon.Up);
        }

        static string Gb(ulong b) { return (b / 1073741824.0).ToString("0.0") + " GB"; }

        // ---------- tray icon: follows the *taskbar's* light/dark mode (not the app theme), like built-in Windows icons
        static bool LightTaskbar()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                { var v = k == null ? null : k.GetValue("SystemUsesLightTheme"); return v != null && Convert.ToInt32(v) == 1; }
            }
            catch { return false; }
        }

        void Refresh()
        {
            if (!Settings.ShowTrayIcon) return;
            if (++themeCheck % 15 == 0) { bool l = LightTaskbar(); if (l != lightTaskbar) { lightTaskbar = l; lastKey = null; } }

            Color fg = lightTaskbar ? Color.FromArgb(24, 24, 24) : Color.White;
            Color warn = lightTaskbar ? Color.FromArgb(196, 104, 0) : Color.FromArgb(255, 176, 32);
            Color bad = lightTaskbar ? Color.FromArgb(196, 28, 28) : Color.FromArgb(255, 86, 76);
            string val, unit; double pct = -1; int arrow = 0;
            switch (Settings.TrayMetric)
            {
                case "temp":
                    unit = Settings.Fahrenheit ? "\u00B0F" : "\u00B0C";
                    if (double.IsNaN(mon.TempC)) val = "--";
                    else { val = Math.Round(LiveMonitor.TempVal(mon.TempC)).ToString("0"); pct = mon.TempC >= 80 ? 100 : mon.TempC >= 65 ? 70 : 0; }
                    break;
                case "ram": val = Math.Round(mon.RamPct).ToString("0"); unit = "%"; pct = mon.RamPct; break;
                case "down": TrayIcon.NetParts(mon.Down, out val, out unit); arrow = 1; break;
                case "up": TrayIcon.NetParts(mon.Up, out val, out unit); arrow = 2; break;
                default: val = Math.Round(mon.Cpu).ToString("0"); unit = "%"; pct = mon.Cpu; break;
            }
            Color vc = pct >= 85 ? bad : pct >= 60 ? warn : fg;
            string key = val + "|" + unit + "|" + arrow + "|" + vc.ToArgb() + "|" + fg.ToArgb();
            if (key != lastKey)
            {
                int sz = Math.Max(16, SystemInformation.SmallIconSize.Width);
                Icon ic;
                using (var bmp = TrayIcon.Render(sz, val, unit, arrow, vc, Color.FromArgb(225, fg))) ic = TrayIcon.ToIcon(bmp);
                ni.Icon = ic; if (curIcon != null) curIcon.Dispose(); curIcon = ic; lastKey = key;
            }
            string tip = "CPU " + mon.Cpu.ToString("0") + "% \u00B7 RAM " + mon.RamPct.ToString("0") + "%" +
                (double.IsNaN(mon.TempC) ? "" : " \u00B7 " + LiveMonitor.Temp(mon.TempC)) +
                "\n\u2193 " + LiveMonitor.Rate(mon.Down) + "  \u2191 " + LiveMonitor.Rate(mon.Up);
            ni.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
            if (menu.Visible) UpdateInfo();
        }

        // ---------- windows
        public void ShowMain()
        {
            if (main == null || main.IsDisposed) main = new MainForm(this);
            if (!main.Visible) main.Show();
            if (main.WindowState == FormWindowState.Minimized) main.WindowState = FormWindowState.Normal;
            main.Activate();
        }

        public void ToggleFlyout()
        {
            if (fly == null || fly.IsDisposed) fly = new Flyout(this, mon);
            if (fly.Visible) fly.Hide();
            else if (Environment.TickCount - fly.LastHide > 300) fly.ShowNearTray();
        }

        public void RequestShow() { marshal.BeginInvoke(new Action(ShowMain)); }

        public void HiddenToTray()
        {
            if (Settings.BalloonShown) return;
            Settings.BalloonShown = true;
            if (Settings.ShowTrayIcon)
                ni.ShowBalloonTip(3000, "SpecScope is still running", "Live monitoring continues in the background. You can turn this off in Settings.", ToolTipIcon.Info);
        }

        public void ExitApp()
        {
            if (Exiting) return;
            Exiting = true; timer.Stop(); SystemEvents.UserPreferenceChanged -= prefHandler;
            ni.Visible = false; ni.Dispose();
            if (fly != null) fly.Dispose();
            if (widget != null) widget.Dispose();
            if (main != null && !main.IsDisposed) main.Dispose();
            ExitThread();
        }
    }
}
