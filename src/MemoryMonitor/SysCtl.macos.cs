// SysCtl.macos.cs
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace MemoryMonitor;

public partial class SysCtl
{
    internal static partial bool TryGetUlong_macos(string name, out ulong value)
    {
        value = 0;
        nuint len = 0;
        if (sysctlbyname(name, IntPtr.Zero, ref len, IntPtr.Zero, 0) != 0 || len == 0)
            return false;

        IntPtr buf = Marshal.AllocHGlobal((int)len);
        try
        {
            if (sysctlbyname(name, buf, ref len, IntPtr.Zero, 0) != 0)
                return false;

            if (len == (nuint)sizeof(ulong))
                value = (ulong)Marshal.ReadInt64(buf);
            else if (len == (nuint)sizeof(uint))
                value = (uint)Marshal.ReadInt32(buf);
            else
                value = (ulong)Marshal.ReadInt64(buf);

            return true;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    [DllImport("libSystem.dylib", SetLastError = true)]
    private static extern int sysctlbyname(string name, IntPtr oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);
}
