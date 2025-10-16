// SysCtl.windows.cs
namespace MemoryMonitor;

public static partial class SysCtl
{
    internal static partial bool TryGetUlong_windows(string name, out ulong value)
    {
        value = 0;
        // 我們在 Windows 走 Win32 API，不用 sysctl
        return false;
    }
}
