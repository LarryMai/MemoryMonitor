using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MemoryMonitor
{
    public readonly record struct SystemMemory(
    ulong TotalBytes,
    ulong AvailableBytes,
    ulong UsedBytes,
    double UsedPercent)
    {
        public static SystemMemory Read()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var m = new MEMORYSTATUSEX();
                m.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
                if (!GlobalMemoryStatusEx(ref m)) throw new InvalidOperationException("GlobalMemoryStatusEx failed");
                ulong total = m.ullTotalPhys;
                ulong avail = m.ullAvailPhys;
                ulong used = total - avail;
                double pct = total > 0 ? (double)used / total * 100.0 : 0;
                return new(total, avail, used, pct);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ulong total = 0, avail = 0;
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:")) total = ParseKbLine(line);
                    else if (line.StartsWith("MemAvailable:")) avail = ParseKbLine(line);
                    if (total != 0 && avail != 0) break;
                }
                if (total == 0) throw new InvalidOperationException("/proc/meminfo missing MemTotal");
                if (avail == 0)
                {
                    foreach (var line in File.ReadLines("/proc/meminfo"))
                        if (line.StartsWith("MemFree:")) { avail = ParseKbLine(line); break; }
                }
                ulong used = total - avail;
                double pct = total > 0 ? (double)used / total * 100.0 : 0;
                return new(total, avail, used, pct);

                static ulong ParseKbLine(string line)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) return 0;
                    if (!ulong.TryParse(parts[1], out var kb)) return 0;
                    return kb * 1024UL;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (!SysCtl.TryGetUlong("hw.memsize", out var total))
                    throw new PlatformNotSupportedException("sysctl hw.memsize");
                if (!SysCtl.TryGetUlong("hw.pagesize", out var pageSize))
                    throw new PlatformNotSupportedException("sysctl hw.pagesize");

                SysCtl.TryGetUlong("vm.page_free_count", out var freePages);
                SysCtl.TryGetUlong("vm.page_inactive_count", out var inactivePages);
                SysCtl.TryGetUlong("vm.page_speculative_count", out var speculativePages);

                ulong avail = (freePages + inactivePages + speculativePages) * pageSize;
                if (avail > total) avail = total;
                ulong used = total - avail;
                double pct = total > 0 ? (double)used / total * 100.0 : 0;
                return new(total, avail, used, pct);
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported OS");
            }
        }

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
    }
}
