using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SpecScope
{
    /// <summary>Per-user settings (HKCU), cached in memory - no admin rights needed.</summary>
    static class Settings
    {
        const string Key = @"Software\SpecScope";
        const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const int Unset = int.MinValue;
        static bool loaded;
        static string metric = "cpu", theme = "dark";
        static bool closeToTray = true, showTray = true, fahr, balloon, widget, widgetTop = true;
        static int interval = 1000, wOpacity = 100, wx = Unset, wy = Unset;
        static bool? start;

        static object Get(string n) { try { using (var k = Registry.CurrentUser.OpenSubKey(Key)) return k == null ? null : k.GetValue(n); } catch { return null; } }
        static void Set(string n, object v) { try { using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue(n, v); } catch { } }
        static bool B(string n, bool d) { var v = Get(n); return v == null ? d : Convert.ToInt32(v) != 0; }
        static int I(string n, int d) { var v = Get(n); try { return v == null ? d : Convert.ToInt32(v); } catch { return d; } }

        static void Load()
        {
            if (loaded) return; loaded = true;
            var m = Get("TrayMetric") as string; if (!string.IsNullOrEmpty(m)) metric = m;
            var t = Get("Theme") as string; if (!string.IsNullOrEmpty(t)) theme = t;
            closeToTray = B("CloseToTray", true); showTray = B("ShowTrayIcon", true); fahr = B("Fahrenheit", false); balloon = B("BalloonShown", false);
            widget = B("ShowWidget", false); widgetTop = B("WidgetOnTop", true);
            int iv = I("IntervalMs", 1000); if (iv == 1000 || iv == 2000 || iv == 5000) interval = iv;
            int op = I("WidgetOpacity", 100); if (op == 100 || op == 85 || op == 70) wOpacity = op;
            wx = I("WidgetX", Unset); wy = I("WidgetY", Unset);
        }

        public static string TrayMetric { get { Load(); return metric; } set { Load(); metric = value; Set("TrayMetric", value); } }
        public static string ThemeKey { get { Load(); return theme; } set { Load(); theme = value; Set("Theme", value); } }
        public static bool CloseToTray { get { Load(); return closeToTray; } set { Load(); closeToTray = value; Set("CloseToTray", value ? 1 : 0); } }
        public static bool ShowTrayIcon { get { Load(); return showTray; } set { Load(); showTray = value; Set("ShowTrayIcon", value ? 1 : 0); } }
        public static bool Fahrenheit { get { Load(); return fahr; } set { Load(); fahr = value; Set("Fahrenheit", value ? 1 : 0); } }
        public static bool BalloonShown { get { Load(); return balloon; } set { Load(); balloon = value; Set("BalloonShown", value ? 1 : 0); } }
        public static bool ShowWidget { get { Load(); return widget; } set { Load(); widget = value; Set("ShowWidget", value ? 1 : 0); } }
        public static bool WidgetOnTop { get { Load(); return widgetTop; } set { Load(); widgetTop = value; Set("WidgetOnTop", value ? 1 : 0); } }
        public static int WidgetOpacity { get { Load(); return wOpacity; } set { Load(); wOpacity = value; Set("WidgetOpacity", value); } }
        public static int IntervalMs { get { Load(); return interval; } set { Load(); interval = value; Set("IntervalMs", value); } }
        public static bool HasWidgetPos { get { Load(); return wx != Unset && wy != Unset; } }
        public static int WidgetX { get { Load(); return wx; } }
        public static int WidgetY { get { Load(); return wy; } }
        public static void SetWidgetPos(int x, int y) { Load(); wx = x; wy = y; Set("WidgetX", x); Set("WidgetY", y); }

        /// <summary>The app may keep running without a window when the tray icon or the desktop widget is on.</summary>
        public static bool CanRunInBackground { get { return ShowTrayIcon || ShowWidget; } }

        public static bool StartWithWindows
        {
            get
            {
                if (start == null)
                { try { using (var k = Registry.CurrentUser.OpenSubKey(Run)) start = k != null && k.GetValue(AppInfo.Name) != null; } catch { start = false; } }
                return start.Value;
            }
            set
            {
                start = value;
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(Run))
                    {
                        if (value) k.SetValue(AppInfo.Name, "\"" + Application.ExecutablePath + "\" /tray");
                        else k.DeleteValue(AppInfo.Name, false);
                    }
                }
                catch { }
            }
        }
    }
}
