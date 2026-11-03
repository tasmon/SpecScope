using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SpecScope
{
    static class AppInfo
    {
        public const string Name = "SpecScope";
        public const string Developer = "Tasmon Islam";
        public const string Email = "tasmon@outlook.com";
        public static string Version
        {
            get { var v = typeof(AppInfo).Assembly.GetName().Version; return v.Major + "." + v.Minor + "." + v.Build; }
        }
        public static bool IsAdmin()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }
    }

    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct FT { public uint Lo, Hi; public ulong V { get { return ((ulong)Hi << 32) | Lo; } } }

        [StructLayout(LayoutKind.Sequential)]
        public class MEMSTATUS
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMSTATUS));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out FT idle, out FT kernel, out FT user);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GlobalMemoryStatusEx([In, Out] MEMSTATUS m);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        /// <summary>Dark/light title bar; on Windows 11 also tints the caption and border to match the theme.</summary>
        public static void Chrome(IntPtr h, System.Drawing.Color bg, System.Drawing.Color fg, bool dark)
        {
            int v = dark ? 1 : 0;
            try { if (DwmSetWindowAttribute(h, 20, ref v, 4) != 0) DwmSetWindowAttribute(h, 19, ref v, 4); } catch { }
            try
            {
                int c = bg.R | (bg.G << 8) | (bg.B << 16);
                DwmSetWindowAttribute(h, 35, ref c, 4);   // caption colour
                DwmSetWindowAttribute(h, 34, ref c, 4);   // border colour
                int t = fg.R | (fg.G << 8) | (fg.B << 16);
                DwmSetWindowAttribute(h, 36, ref t, 4);   // caption text colour
            }
            catch { }
        }
        public static void RoundCorners(IntPtr h)
        {
            int v = 2;
            try { DwmSetWindowAttribute(h, 33, ref v, 4); } catch { }
        }
    }
}
