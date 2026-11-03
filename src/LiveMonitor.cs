using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SpecScope
{
    /// <summary>Samples CPU, per-core load, RAM, disk, GPU, network, battery and temperature once per Tick().</summary>
    class LiveMonitor
    {
        public static readonly LiveMonitor I = new LiveMonitor();
        public const int N = 60;
        public int IntervalMs = 1000;

        public double Cpu, RamPct, Down, Up, TempC = double.NaN, DiskActive, DiskRead, DiskWrite;
        public double GpuLoad = double.NaN, CpuMhz, CpuBaseMhz, BatteryPct = double.NaN;
        public ulong RamUsed, RamTotal, GpuMem;
        public double[] Cores = new double[0];
        public bool HasBattery, Charging;
        public readonly List<double> CpuH = new List<double>(), RamH = new List<double>(), DownH = new List<double>(),
            UpH = new List<double>(), TempH = new List<double>(), DiskH = new List<double>(), DiskRH = new List<double>(),
            DiskWH = new List<double>(), GpuH = new List<double>(), GpuMH = new List<double>(), BatH = new List<double>();

        readonly Stopwatch sw = Stopwatch.StartNew();
        public double Frac { get { return Math.Min(1.0, sw.ElapsedMilliseconds / (double)Math.Max(100, IntervalMs)); } }

        ulong pIdle, pKern, pUser; bool havePrev;
        long pRx, pTx; DateTime pT;
        int tick; bool started;
        volatile bool tempBusy, gpuBusy, countersReady;
        double tempCache = double.NaN, gpuLoadCache = double.NaN; ulong gpuMemCache;
        PerformanceCounter[] coreC; PerformanceCounter pcDisk, pcRd, pcWr, pcPerf;

        void InitCounters()
        {
            try
            {
                var names = new PerformanceCounterCategory("Processor").GetInstanceNames().Where(n => n != "_Total")
                    .OrderBy(n => { int x; return int.TryParse(n, out x) ? x : 0; }).ToArray();
                var cs = names.Select(n => new PerformanceCounter("Processor", "% Processor Time", n, true)).ToArray();
                foreach (var c in cs) c.NextValue();
                coreC = cs;
            }
            catch { }
            try
            {
                pcDisk = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
                pcRd = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                pcWr = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
                pcDisk.NextValue(); pcRd.NextValue(); pcWr.NextValue();
            }
            catch { pcDisk = pcRd = pcWr = null; }
            try { pcPerf = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true); pcPerf.NextValue(); }
            catch { pcPerf = null; }
            countersReady = true;
        }

        public void Tick()
        {
            if (!started) { started = true; Task.Factory.StartNew(InitCounters); }
            sw.Reset(); sw.Start();

            Native.FT i, k, u;
            if (Native.GetSystemTimes(out i, out k, out u))
            {
                if (havePrev)
                {
                    ulong di = i.V - pIdle, tot = (k.V - pKern) + (u.V - pUser);
                    if (tot > 0) Cpu = Math.Max(0, Math.Min(100, 100.0 * (tot - di) / tot));
                }
                pIdle = i.V; pKern = k.V; pUser = u.V; havePrev = true;
            }

            var m = new Native.MEMSTATUS();
            if (Native.GlobalMemoryStatusEx(m))
            { RamTotal = m.ullTotalPhys; RamUsed = m.ullTotalPhys - m.ullAvailPhys; RamPct = m.dwMemoryLoad; }

            if (countersReady)
            {
                try { if (coreC != null) Cores = coreC.Select(c => Math.Min(100.0, (double)c.NextValue())).ToArray(); } catch { }
                try
                {
                    if (pcDisk != null)
                    { DiskActive = Math.Min(100, pcDisk.NextValue()); DiskRead = pcRd.NextValue(); DiskWrite = pcWr.NextValue(); }
                }
                catch { }
                try { if (pcPerf != null && CpuBaseMhz > 0) CpuMhz = CpuBaseMhz * pcPerf.NextValue() / 100.0; } catch { }
            }

            long rx = 0, tx = 0;
            try
            {
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.OperationalStatus != OperationalStatus.Up) continue;
                    var t = n.NetworkInterfaceType;
                    if (t == NetworkInterfaceType.Loopback || t == NetworkInterfaceType.Tunnel) continue;
                    try { var st = n.GetIPv4Statistics(); rx += st.BytesReceived; tx += st.BytesSent; } catch { }
                }
            }
            catch { }
            var now = DateTime.UtcNow;
            if (pT != default(DateTime))
            {
                double dt = (now - pT).TotalSeconds;
                if (dt > 0.1) { Down = Math.Max(0, (rx - pRx) / dt); Up = Math.Max(0, (tx - pTx) / dt); }
            }
            pRx = rx; pTx = tx; pT = now;

            try
            {
                var ps = SystemInformation.PowerStatus;
                HasBattery = (ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0 && ps.BatteryChargeStatus != BatteryChargeStatus.Unknown;
                BatteryPct = HasBattery ? Math.Min(100, ps.BatteryLifePercent * 100.0) : double.NaN;
                Charging = ps.PowerLineStatus == PowerLineStatus.Online;
            }
            catch { }

            if (tick % 4 == 0 && !tempBusy)
            {
                tempBusy = true;
                Task.Factory.StartNew(() => { try { tempCache = ReadTemp(); } catch { } finally { tempBusy = false; } });
            }
            if (tick % 2 == 0 && !gpuBusy)
            {
                gpuBusy = true;
                Task.Factory.StartNew(() => { try { double l; ulong mem; ReadGpu(out l, out mem); gpuLoadCache = l; gpuMemCache = mem; } catch { } finally { gpuBusy = false; } });
            }
            TempC = tempCache; GpuLoad = gpuLoadCache; GpuMem = gpuMemCache;
            tick++;

            Push(CpuH, Cpu); Push(RamH, RamPct); Push(DownH, Down); Push(UpH, Up);
            Push(TempH, double.IsNaN(TempC) ? 0 : TempC);
            Push(DiskH, DiskActive); Push(DiskRH, DiskRead); Push(DiskWH, DiskWrite);
            Push(GpuH, double.IsNaN(GpuLoad) ? 0 : GpuLoad); Push(GpuMH, GpuMem);
            Push(BatH, double.IsNaN(BatteryPct) ? 0 : BatteryPct);
        }

        static void Push(List<double> l, double v) { l.Add(v); if (l.Count > N) l.RemoveAt(0); }

        /// <summary>Best-effort: returns NaN when the hardware/firmware doesn't expose a thermal zone.</summary>
        static double ReadTemp()
        {
            double best = double.NaN;
            foreach (var o in W.Q("SELECT Temperature, HighPrecisionTemperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation"))
            {
                double c = double.NaN;
                double hp = W.U(o, "HighPrecisionTemperature");
                if (hp > 2000) c = hp / 10.0 - 273.15;
                else { double kk = W.U(o, "Temperature"); if (kk > 200) c = kk - 273.15; }
                if (c > 5 && c < 125 && (double.IsNaN(best) || c > best)) best = c;
            }
            if (double.IsNaN(best))
                foreach (var o in W.Q("SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature", @"root\WMI"))
                {
                    double c = W.U(o, "CurrentTemperature") / 10.0 - 273.15;
                    if (c > 5 && c < 125 && (double.IsNaN(best) || c > best)) best = c;
                }
            return best;
        }

        /// <summary>GPU engine utilisation (busiest engine, like Task Manager) and dedicated VRAM in use, via Windows' GPU counters.</summary>
        static void ReadGpu(out double load, out ulong mem)
        {
            load = double.NaN; mem = 0;
            var sums = new Dictionary<string, double>();
            foreach (var o in W.Q("SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine"))
            {
                string n = W.S(o, "Name"); if (n == null) continue;
                int a = n.IndexOf("luid_", StringComparison.Ordinal), b = n.IndexOf("_phys", StringComparison.Ordinal), e = n.IndexOf("_eng_", StringComparison.Ordinal);
                string key = (a >= 0 && b > a ? n.Substring(a, b - a) : "gpu") + "|" + (e >= 0 ? n.Substring(e) : n);
                double cur; sums.TryGetValue(key, out cur); sums[key] = cur + W.U(o, "UtilizationPercentage");
            }
            if (sums.Count > 0) load = Math.Min(100, sums.Values.Max());
            foreach (var o in W.Q("SELECT DedicatedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory"))
            { ulong u = W.U(o, "DedicatedUsage"); if (u > mem) mem = u; }
        }

        public static string Rate(double b)
        {
            if (b < 1024) return b.ToString("0") + " B/s";
            if (b < 1048576) return (b / 1024).ToString("0") + " KB/s";
            if (b < 1073741824) return (b / 1048576).ToString("0.0") + " MB/s";
            return (b / 1073741824).ToString("0.00") + " GB/s";
        }

        public static double TempVal(double c) { return Settings.Fahrenheit ? c * 9 / 5 + 32 : c; }
        public static string Temp(double c) { return TempVal(c).ToString("0") + (Settings.Fahrenheit ? "\u00B0F" : "\u00B0C"); }
    }
}
