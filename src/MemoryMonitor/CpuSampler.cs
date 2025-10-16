using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MemoryMonitor
{
    public readonly record struct CpuSnapshot(
        double OverallUsageRatio,
        int LogicalProcessorCount,
        double[]? PerCoreUsageRatios);

    internal sealed class CpuSampler
    {
        private ICpuReader _reader;

        public CpuSampler()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                _reader = new LinuxCpuReader();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                _reader = new MacCpuReader();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _reader = new WindowsCpuReader();
            else
                throw new PlatformNotSupportedException("Unsupported OS for CPU sampler");
        }

        public bool TryPrime() => _reader.TryPrime();
        public CpuSnapshot Sample() => _reader.Sample();
    }

    internal interface ICpuReader
    {
        bool TryPrime();
        CpuSnapshot Sample();
    }

    // ---- Linux CPU reader (/proc/stat) ----
    internal sealed class LinuxCpuReader : ICpuReader
    {
        private CpuTicks? _prevAll;
        private CpuTicks[]? _prevCores;
        private int _logical;

        public bool TryPrime()
        {
            (_prevAll, _prevCores, _logical) = ReadTicks();
            return _prevAll is not null;
        }

        public CpuSnapshot Sample()
        {
            var (nowAll, nowCores, logical) = ReadTicks();
            double overall = UsageDelta(_prevAll!.Value, nowAll!.Value);
            double[]? perCore = null;
            if (nowCores is not null && _prevCores is not null)
            {
                int n = Math.Min(nowCores.Length, _prevCores.Length);
                perCore = new double[n];
                for (int i = 0; i < n; i++) perCore[i] = UsageDelta(_prevCores[i], nowCores[i]);
            }
            _prevAll = nowAll; _prevCores = nowCores; _logical = logical;
            return new CpuSnapshot(overall, logical, perCore);
        }

        private static (CpuTicks? all, CpuTicks[]? cores, int logical) ReadTicks()
        {
            CpuTicks? all = null;
            var cores = new List<CpuTicks>(64);
            foreach (var line in File.ReadLines("/proc/stat"))
            {
                if (line.StartsWith("cpu "))
                {
                    var t = ParseLine(line); all = t;
                }
                else if (line.StartsWith("cpu"))
                {
                    var t = ParseLine(line); cores.Add(t);
                }
                else if (line.StartsWith("intr")) break; // done with cpu lines
            }
            int logical = cores.Count > 0 ? cores.Count : Environment.ProcessorCount;
            return (all, cores.Count > 0 ? cores.ToArray() : null, logical);

            static CpuTicks ParseLine(string l)
            {
                // cpu  user nice system idle iowait irq softirq steal guest guest_nice
                var parts = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                ulong user = ulong.Parse(parts[1]);
                ulong nice = ulong.Parse(parts[2]);
                ulong system = ulong.Parse(parts[3]);
                ulong idle = ulong.Parse(parts[4]);
                ulong iowait = parts.Length > 5 ? ulong.Parse(parts[5]) : 0UL;
                ulong irq = parts.Length > 6 ? ulong.Parse(parts[6]) : 0UL;
                ulong softirq = parts.Length > 7 ? ulong.Parse(parts[7]) : 0UL;
                ulong steal = parts.Length > 8 ? ulong.Parse(parts[8]) : 0UL;
                return new CpuTicks(user, nice, system, idle, iowait, irq, softirq, steal);
            }
        }

        private static double UsageDelta(CpuTicks a, CpuTicks b)
        {
            double idleA = a.Idle + a.IOWait;
            double idleB = b.Idle + b.IOWait;
            double idleDelta = idleB - idleA;
            double totalA = a.Total;
            double totalB = b.Total;
            double totalDelta = totalB - totalA;
            if (totalDelta <= 0) return 0;
            double usage = 1.0 - (idleDelta / totalDelta);
            if (usage < 0) usage = 0; if (usage > 1) usage = 1;
            return usage;
        }

        private readonly record struct CpuTicks(ulong User, ulong Nice, ulong System, ulong Idle, ulong IOWait, ulong IRQ, ulong SoftIRQ, ulong Steal)
        {
            public double Total => (double)(User + Nice + System + Idle + IOWait + IRQ + SoftIRQ + Steal);
        }
    }

    // ---- macOS CPU reader (sysctl kern.cp_time) ----
    internal sealed class MacCpuReader : ICpuReader
    {
        private CpuTimes? _prev;
        private int _logical = Environment.ProcessorCount;

        public bool TryPrime()
        {
            _prev = Read();
            return _prev is not null;
        }

        public CpuSnapshot Sample()
        {
            var now = Read();
            var prev = _prev!.Value;
            double overall = Usage(prev, now);
            _prev = now;
            return new CpuSnapshot(overall, _logical, null);
        }

        private static CpuTimes Read()
        {
            // kern.cp_time returns 5 longs: user, nice, sys, idle, intr (on some systems)
            // We'll fetch via sysctlbyname twice: once to get length, then read buffer of 5 * sizeof(long)
            var arr = SysCtlLongArray("kern.cp_time");
            ulong user = arr.Length > 0 ? (ulong)arr[0] : 0UL;
            ulong nice = arr.Length > 1 ? (ulong)arr[1] : 0UL;
            ulong sys = arr.Length > 2 ? (ulong)arr[2] : 0UL;
            ulong idle = arr.Length > 3 ? (ulong)arr[3] : 0UL;
            ulong intr = arr.Length > 4 ? (ulong)arr[4] : 0UL;
            return new CpuTimes(user, nice, sys, idle, intr);
        }

        private static double Usage(CpuTimes a, CpuTimes b)
        {
            double idleDelta = (b.Idle - a.Idle);
            double totalDelta = (b.Total - a.Total);
            if (totalDelta <= 0) return 0;
            double usage = 1.0 - (idleDelta / totalDelta);
            if (usage < 0) usage = 0; if (usage > 1) usage = 1;
            return usage;
        }

        private readonly record struct CpuTimes(ulong User, ulong Nice, ulong Sys, ulong Idle, ulong Intr)
        {
            public double Total => (double)(User + Nice + Sys + Idle + Intr);
        }

        private static long[] SysCtlLongArray(string name)
        {
            nuint len = 0;
            SysCtlRaw(name, IntPtr.Zero, ref len);
            if (len == 0) return Array.Empty<long>();
            IntPtr buf = Marshal.AllocHGlobal((int)len);
            try
            {
                if (SysCtlRaw(name, buf, ref len) != 0) return Array.Empty<long>();
                int count = (int)(len / (nuint)sizeof(long));
                var arr = new long[count];
                for (int i = 0; i < count; i++) arr[i] = Marshal.ReadInt64(buf, i * sizeof(long));
                return arr;
            }
            finally { Marshal.FreeHGlobal(buf); }

            static int SysCtlRaw(string n, IntPtr oldp, ref nuint oldlenp)
                => sysctlbyname(n, oldp, ref oldlenp, IntPtr.Zero, 0);
        }

        [DllImport("libSystem.dylib", SetLastError = true)]
        private static extern int sysctlbyname(string name, IntPtr oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);
    }

    // ---- Windows CPU reader (GetSystemTimes) ----
    internal sealed class WindowsCpuReader : ICpuReader
    {
        private FILETIME _prevIdle, _prevKernel, _prevUser;
        private bool _primed;

        public bool TryPrime()
        {
            _primed = GetSystemTimes(out _prevIdle, out _prevKernel, out _prevUser);
            return _primed;
        }

        public CpuSnapshot Sample()
        {
            if (!_primed) TryPrime();
            GetSystemTimes(out var idle, out var kernel, out var user);

            double idleDelta = Sub(idle, _prevIdle);
            double kernelDelta = Sub(kernel, _prevKernel);
            double userDelta = Sub(user, _prevUser);
            double totalDelta = kernelDelta + userDelta;
            // kernel includes idle on Windows; subtract idleDelta from kernelDelta
            double busyDelta = (kernelDelta - idleDelta) + userDelta;
            double usage = totalDelta <= 0 ? 0 : busyDelta / totalDelta;
            if (usage < 0) usage = 0; if (usage > 1) usage = 1;

            _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
            return new CpuSnapshot(usage, Environment.ProcessorCount, null);

            static double Sub(FILETIME a, FILETIME b)
            {
                ulong ua = ((ulong)a.dwHighDateTime << 32) | (uint)a.dwLowDateTime;
                ulong ub = ((ulong)b.dwHighDateTime << 32) | (uint)b.dwLowDateTime;
                return ua > ub ? (double)(ua - ub) : 0.0;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }
    }
}
