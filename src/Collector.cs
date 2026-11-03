using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SpecScope
{
    class Row
    {
        public string Group, Key, Value;
        public Func<string> Live;      // when set, the value is recomputed on every repaint
        public float H, HW;            // cached layout height / width it was measured for
        public string Text { get { return Live != null ? Live() : Value; } }
    }

    class Section
    {
        public string Name;
        public List<Row> Rows = new List<Row>();
        public bool Special;           // Settings page – not backed by rows
        public Section(string n) { Name = n; }
        public void AddLive(string g, string k, Func<string> f) { Rows.Add(new Row { Group = g, Key = k, Value = f(), Live = f }); }
        public void Add(string g, string k, string v)
        {
            if (!string.IsNullOrWhiteSpace(v)) Rows.Add(new Row { Group = g, Key = k, Value = v.Trim() });
        }
    }

    static class W
    {
        public static List<ManagementObject> Q(string q, string scope = @"root\cimv2")
        {
            var l = new List<ManagementObject>();
            try { using (var s = new ManagementObjectSearcher(scope, q)) foreach (ManagementObject o in s.Get()) l.Add(o); }
            catch { }
            return l;
        }
        public static string S(ManagementBaseObject o, string p)
        { try { var v = o[p]; return v == null ? null : v.ToString().Trim(); } catch { return null; } }
        public static ulong U(ManagementBaseObject o, string p)
        { ulong r; ulong.TryParse(S(o, p), out r); return r; }
        public static string Date(ManagementBaseObject o, string p)
        {
            try { var s = S(o, p); if (string.IsNullOrEmpty(s)) return null; return ManagementDateTimeConverter.ToDateTime(s).ToString("yyyy-MM-dd HH:mm"); }
            catch { return null; }
        }
        public static string Bytes(double b)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" }; int i = 0;
            while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
            return (i >= 3 ? b.ToString("0.##") : b.ToString("0")) + " " + u[i];
        }
        public static string Reg(string path, string name)
        {
            try { using (var k = Registry.LocalMachine.OpenSubKey(path)) { var v = k == null ? null : k.GetValue(name); return v == null ? null : v.ToString(); } }
            catch { return null; }
        }
    }

    static class Collector
    {
        public static List<Section> All()
        {
            var sum = new Section("Summary");
            var list = new List<Section> { sum };
            list.Add(Os(sum)); list.Add(Cpu(sum)); list.Add(Ram(sum)); list.Add(Board(sum));
            list.Add(Gpu(sum)); list.Add(Storage(sum)); list.Add(Audio(sum)); list.Add(Net(sum));
            list.Add(Periph()); list.Add(Sensors());
            return list;
        }

        static Section Os(Section sum)
        {
            var s = new Section("Operating System");
            foreach (var o in W.Q("SELECT * FROM Win32_OperatingSystem"))
            {
                string disp = W.Reg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
                string ubr = W.Reg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR");
                string cap = (W.S(o, "Caption") ?? "Windows").Replace("Microsoft ", "");
                string build = W.S(o, "BuildNumber") + (ubr != null ? "." + ubr : "");
                s.Add(null, "Edition", cap);
                s.Add(null, "Version", disp);
                s.Add(null, "Build", build);
                s.Add(null, "Architecture", W.S(o, "OSArchitecture"));
                s.Add(null, "Installed", W.Date(o, "InstallDate"));
                string boot = W.Date(o, "LastBootUpTime");
                s.Add(null, "Last boot", boot);
                try
                {
                    var bt = ManagementDateTimeConverter.ToDateTime(W.S(o, "LastBootUpTime"));
                    s.AddLive(null, "Uptime", () => { var up = DateTime.Now - bt; return string.Format("{0}d {1}h {2}m {3}s", up.Days, up.Hours, up.Minutes, up.Seconds); });
                }
                catch { }
                s.Add(null, "Computer name", Environment.MachineName);
                s.Add(null, "User", Environment.UserName);
                s.Add(null, "Registered owner", W.S(o, "RegisteredUser"));
                s.Add(null, "System directory", W.S(o, "SystemDirectory"));
                s.Add(null, "Secure Boot", W.Reg(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled") == "1" ? "Enabled" : (W.Reg(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled") == "0" ? "Disabled" : "Not supported / legacy BIOS"));
                sum.Add(null, "Operating System", cap + " " + disp + " " + W.S(o, "OSArchitecture") + " (build " + build + ")");
                break;
            }
            return s;
        }

        static Section Cpu(Section sum)
        {
            var s = new Section("CPU");
            int n = 0;
            foreach (var o in W.Q("SELECT * FROM Win32_Processor"))
            {
                n++; string g = "Processor " + n;
                string name = Regex1(W.S(o, "Name"));
                s.Add(g, "Name", name);
                s.Add(g, "Manufacturer", W.S(o, "Manufacturer"));
                s.Add(g, "Cores", W.S(o, "NumberOfCores"));
                s.Add(g, "Threads", W.S(o, "NumberOfLogicalProcessors"));
                s.Add(g, "Base clock", W.S(o, "MaxClockSpeed") + " MHz");
                string cc = W.S(o, "CurrentClockSpeed") + " MHz";
                LiveMonitor.I.CpuBaseMhz = W.U(o, "MaxClockSpeed");
                s.AddLive(g, "Current clock", () => LiveMonitor.I.CpuMhz > 0 ? LiveMonitor.I.CpuMhz.ToString("0") + " MHz" : cc);
                s.Add(g, "Socket", W.S(o, "SocketDesignation"));
                ulong l2 = W.U(o, "L2CacheSize"), l3 = W.U(o, "L3CacheSize");
                if (l2 > 0) s.Add(g, "L2 cache", W.Bytes(l2 * 1024.0));
                if (l3 > 0) s.Add(g, "L3 cache", W.Bytes(l3 * 1024.0));
                s.Add(g, "Virtualization", W.S(o, "VirtualizationFirmwareEnabled") == "True" ? "Enabled" : (W.S(o, "VirtualizationFirmwareEnabled") == "False" ? "Disabled" : null));
                s.AddLive(g, "Load", () => LiveMonitor.I.Cpu.ToString("0") + " %");
                s.Add(g, "Description", W.S(o, "Description"));
                sum.Add(null, "CPU", name + " (" + W.S(o, "NumberOfCores") + " cores / " + W.S(o, "NumberOfLogicalProcessors") + " threads)");
            }
            return s;
        }

        static string Regex1(string s) { return s == null ? null : System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " "); }

        static string MemType(ManagementObject o)
        {
            string t = W.S(o, "SMBIOSMemoryType"); if (t == null || t == "0") t = W.S(o, "MemoryType");
            switch (t)
            {
                case "20": return "DDR"; case "21": return "DDR2"; case "24": return "DDR3";
                case "26": return "DDR4"; case "34": return "DDR5"; case "35": return "LPDDR5"; case "30": return "LPDDR4";
                default: return null;
            }
        }

        static Section Ram(Section sum)
        {
            var s = new Section("RAM");
            ulong total = 0, free = 0, vtotal = 0, vfree = 0;
            foreach (var o in W.Q("SELECT * FROM Win32_OperatingSystem"))
            { free = W.U(o, "FreePhysicalMemory") * 1024; vtotal = W.U(o, "TotalVirtualMemorySize") * 1024; vfree = W.U(o, "FreeVirtualMemory") * 1024; }
            foreach (var o in W.Q("SELECT * FROM Win32_ComputerSystem")) total = W.U(o, "TotalPhysicalMemory");
            s.Add("Memory", "Total physical", W.Bytes(total));
            s.AddLive("Memory", "Available", () => W.Bytes(LiveMonitor.I.RamTotal - LiveMonitor.I.RamUsed));
            s.AddLive("Memory", "In use", () => string.Format("{0} ({1:0}%)", W.Bytes(LiveMonitor.I.RamUsed), LiveMonitor.I.RamPct));
            s.Add("Memory", "Virtual (commit) total", W.Bytes(vtotal));
            s.Add("Memory", "Virtual available", W.Bytes(vfree));
            string type = null, speed = null; int i = 0;
            foreach (var o in W.Q("SELECT * FROM Win32_PhysicalMemory"))
            {
                i++; string g = "Module " + i + (W.S(o, "DeviceLocator") != null ? " – " + W.S(o, "DeviceLocator") : "");
                string t = MemType(o); if (type == null) type = t;
                string sp = W.S(o, "ConfiguredClockSpeed"); if (sp == null || sp == "0") sp = W.S(o, "Speed"); if (speed == null) speed = sp;
                s.Add(g, "Capacity", W.Bytes(W.U(o, "Capacity")));
                s.Add(g, "Type", t);
                s.Add(g, "Speed", sp + " MT/s");
                s.Add(g, "Manufacturer", W.S(o, "Manufacturer"));
                s.Add(g, "Part number", W.S(o, "PartNumber"));
                s.Add(g, "Serial number", W.S(o, "SerialNumber"));
                s.Add(g, "Bank", W.S(o, "BankLabel"));
            }
            sum.Add(null, "RAM", W.Bytes(total) + (type != null ? " " + type : "") + (speed != null ? " @ " + speed + " MT/s" : "") + (i > 0 ? " (" + i + " module" + (i > 1 ? "s" : "") + ")" : ""));
            return s;
        }

        static Section Board(Section sum)
        {
            var s = new Section("Motherboard");
            string bm = null, bp = null;
            foreach (var o in W.Q("SELECT * FROM Win32_BaseBoard"))
            {
                bm = W.S(o, "Manufacturer"); bp = W.S(o, "Product");
                s.Add("Motherboard", "Manufacturer", bm); s.Add("Motherboard", "Model", bp);
                s.Add("Motherboard", "Version", W.S(o, "Version")); s.Add("Motherboard", "Serial number", W.S(o, "SerialNumber"));
            }
            foreach (var o in W.Q("SELECT * FROM Win32_ComputerSystem"))
            {
                s.Add("System", "Manufacturer", W.S(o, "Manufacturer")); s.Add("System", "Model", W.S(o, "Model"));
                s.Add("System", "Type", W.S(o, "SystemType"));
                if (bm == null) sum.Add(null, "Motherboard", W.S(o, "Manufacturer") + " " + W.S(o, "Model"));
            }
            foreach (var o in W.Q("SELECT * FROM Win32_BIOS"))
            {
                s.Add("BIOS / UEFI", "Vendor", W.S(o, "Manufacturer")); s.Add("BIOS / UEFI", "Version", W.S(o, "SMBIOSBIOSVersion"));
                s.Add("BIOS / UEFI", "Release date", W.Date(o, "ReleaseDate"));
            }
            if (bm != null) sum.Add(null, "Motherboard", bm + " " + bp);
            return s;
        }

        static string Vram(string name, ulong fallback)
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                    foreach (var sub in k.GetSubKeyNames())
                        using (var sk = k.OpenSubKey(sub))
                        {
                            if (sk == null) continue;
                            var d = sk.GetValue("DriverDesc") as string;
                            if (d == name)
                            {
                                var v = sk.GetValue("HardwareInformation.qwMemorySize");
                                if (v != null) return W.Bytes(Convert.ToDouble(v));
                            }
                        }
            }
            catch { }
            return fallback > 0 ? W.Bytes(fallback) + (fallback >= 4294967295UL ? "+" : "") : null;
        }

        static Section Gpu(Section sum)
        {
            var s = new Section("Graphics");
            foreach (var o in W.Q("SELECT * FROM Win32_VideoController"))
            {
                string name = W.S(o, "Name"); string g = name ?? "Adapter";
                string vr = Vram(name, W.U(o, "AdapterRAM"));
                s.Add(g, "Name", name); s.Add(g, "Vendor", W.S(o, "AdapterCompatibility"));
                s.Add(g, "Video memory", vr);
                s.Add(g, "Driver version", W.S(o, "DriverVersion")); s.Add(g, "Driver date", W.Date(o, "DriverDate"));
                if (W.U(o, "CurrentHorizontalResolution") > 0)
                {
                    s.Add(g, "Current mode", W.S(o, "CurrentHorizontalResolution") + " x " + W.S(o, "CurrentVerticalResolution") + " @ " + W.S(o, "CurrentRefreshRate") + " Hz");
                    s.Add(g, "Color depth", W.S(o, "CurrentBitsPerPixel") + " bit");
                }
                s.Add(g, "Status", W.S(o, "Status"));
                sum.Add(null, "Graphics", name + (vr != null ? " (" + vr + ")" : ""));
            }
            int i = 0;
            foreach (var sc in Screen.AllScreens)
            {
                i++; string g = "Monitor " + i + (sc.Primary ? " (primary)" : "");
                s.Add(g, "Device", sc.DeviceName);
                s.Add(g, "Resolution", sc.Bounds.Width + " x " + sc.Bounds.Height);
                s.Add(g, "Position", sc.Bounds.X + ", " + sc.Bounds.Y);
            }
            return s;
        }

        static Section Storage(Section sum)
        {
            var s = new Section("Storage");
            var phys = W.Q("SELECT * FROM MSFT_PhysicalDisk", @"root\Microsoft\Windows\Storage");
            foreach (var o in W.Q("SELECT * FROM Win32_DiskDrive"))
            {
                string model = W.S(o, "Model"), g = model ?? "Disk";
                string idx = W.S(o, "Index"); string media = null, bus = null, health = null;
                foreach (var p in phys)
                    if (W.S(p, "DeviceId") == idx)
                    {
                        switch (W.S(p, "MediaType")) { case "3": media = "HDD"; break; case "4": media = "SSD"; break; case "5": media = "SCM"; break; }
                        switch (W.S(p, "BusType")) { case "17": bus = "NVMe"; break; case "11": bus = "SATA"; break; case "7": bus = "USB"; break; case "8": bus = "RAID"; break; case "3": bus = "ATA"; break; }
                        switch (W.S(p, "HealthStatus")) { case "0": health = "Healthy"; break; case "1": health = "Warning"; break; case "2": health = "Unhealthy"; break; }
                    }
                string size = W.Bytes(W.U(o, "Size"));
                s.Add(g, "Model", model); s.Add(g, "Capacity", size); s.Add(g, "Type", media);
                s.Add(g, "Bus", bus ?? W.S(o, "InterfaceType")); s.Add(g, "Health", health);
                s.Add(g, "Firmware", W.S(o, "FirmwareRevision")); s.Add(g, "Serial number", W.S(o, "SerialNumber"));
                s.Add(g, "Partitions", W.S(o, "Partitions"));
                sum.Add(null, "Storage", model + " (" + size + (media != null ? ", " + media : "") + ")");
            }
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    string g = "Volume " + d.Name.TrimEnd(new[] { '\\' });
                    s.Add(g, "Label", d.VolumeLabel); s.Add(g, "Type", d.DriveType.ToString()); s.Add(g, "File system", d.DriveFormat);
                    s.Add(g, "Size", W.Bytes(d.TotalSize));
                    s.Add(g, "Free", string.Format("{0} ({1:0}%)", W.Bytes(d.TotalFreeSpace), 100.0 * d.TotalFreeSpace / d.TotalSize));
                }
                catch { }
            }
            foreach (var o in W.Q("SELECT * FROM Win32_CDROMDrive"))
                s.Add("Optical: " + W.S(o, "Name"), "Drive", W.S(o, "Drive"));
            return s;
        }

        static Section Audio(Section sum)
        {
            var s = new Section("Audio");
            foreach (var o in W.Q("SELECT * FROM Win32_SoundDevice"))
            {
                string n = W.S(o, "Name");
                s.Add(n, "Name", n); s.Add(n, "Manufacturer", W.S(o, "Manufacturer")); s.Add(n, "Status", W.S(o, "Status"));
                sum.Add(null, "Audio", n);
            }
            return s;
        }

        static Section Net(Section sum)
        {
            var s = new Section("Network");
            bool first = true;
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.NetworkInterfaceType == NetworkInterfaceType.Loopback || n.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                string g = n.Name;
                s.Add(g, "Adapter", n.Description); s.Add(g, "Type", n.NetworkInterfaceType.ToString());
                s.Add(g, "Status", n.OperationalStatus.ToString());
                if (n.Speed > 0 && n.Speed < long.MaxValue / 2) s.Add(g, "Link speed", (n.Speed / 1000000) + " Mbps");
                string mac = n.GetPhysicalAddress().ToString();
                if (mac.Length == 12) s.Add(g, "MAC address", string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2))));
                var ip = n.GetIPProperties();
                string v4 = null;
                foreach (var a in ip.UnicastAddresses)
                {
                    if (a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) v4 = a.Address.ToString();
                    else if (!a.Address.IsIPv6LinkLocal) s.Add(g, "IPv6", a.Address.ToString());
                }
                s.Add(g, "IPv4", v4);
                foreach (var gw in ip.GatewayAddresses) if (gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) s.Add(g, "Gateway", gw.Address.ToString());
                var dns = string.Join(", ", ip.DnsAddresses.Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(d => d.ToString()));
                s.Add(g, "DNS", dns);
                if (first && n.OperationalStatus == OperationalStatus.Up && v4 != null)
                { sum.Add(null, "Network", n.Name + " – " + v4); first = false; }
            }
            return s;
        }

        static Section Periph()
        {
            var s = new Section("Peripherals");
            foreach (var o in W.Q("SELECT * FROM Win32_Keyboard")) s.Add("Keyboards", W.S(o, "Name") ?? "Keyboard", W.S(o, "Description"));
            foreach (var o in W.Q("SELECT * FROM Win32_PointingDevice")) s.Add("Mice / pointing", W.S(o, "Name") ?? "Device", W.S(o, "Manufacturer"));
            foreach (var o in W.Q("SELECT * FROM Win32_Printer")) s.Add("Printers", W.S(o, "Name"), W.S(o, "Default") == "True" ? "Default" : "Installed");
            foreach (var o in W.Q("SELECT * FROM Win32_PnPEntity WHERE PNPClass='Camera' OR PNPClass='Image'")) s.Add("Cameras", W.S(o, "Name"), W.S(o, "Status"));
            foreach (var o in W.Q("SELECT * FROM Win32_PnPEntity WHERE PNPClass='Bluetooth' AND Service IS NOT NULL")) s.Add("Bluetooth", W.S(o, "Name"), W.S(o, "Status"));
            return s;
        }

        static Section Sensors()
        {
            var s = new Section("Sensors & Battery");
            int i = 0;
            foreach (var o in W.Q("SELECT * FROM MSAcpi_ThermalZoneTemperature", @"root\WMI"))
            {
                i++; double k = (double)W.U(o, "CurrentTemperature");
                if (k > 0) s.Add("Thermal zones (ACPI)", "Zone " + i, string.Format("{0:0.0} °C", k / 10.0 - 273.15));
            }
            if (i == 0) s.Add("Thermal zones (ACPI)", "Temperature", "Not exposed by this system (try running as administrator)");
            foreach (var o in W.Q("SELECT * FROM Win32_Battery"))
            {
                s.Add("Battery", "Charge", W.S(o, "EstimatedChargeRemaining") + " %");
                string st = W.S(o, "BatteryStatus");
                s.Add("Battery", "State", st == "1" ? "Discharging" : st == "2" ? "On AC power" : st == "3" ? "Fully charged" : st);
                string rt = W.S(o, "EstimatedRunTime"); if (rt != null && rt != "71582788") s.Add("Battery", "Estimated runtime", rt + " min");
            }
            return s;
        }
    }
}
