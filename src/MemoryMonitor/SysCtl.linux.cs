// SysCtl.linux.cs
namespace MemoryMonitor;

public static partial class SysCtl
{
    internal static partial bool TryGetUlong_linux(string name, out ulong value)
    {
        value = 0;
        // 我們在 Linux 走 /proc，不用 sysctl
        return false;
    }
}
