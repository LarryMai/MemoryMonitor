// SysCtl.cs
using System.Runtime.InteropServices;

namespace MemoryMonitor;

public static partial class SysCtl
{
    /// <summary>
    /// 取得 64/32 bit 無號整數 sysctl 值；不支援的平台/鍵回傳 false。
    /// </summary>
    public static bool TryGetUlong(string name, out ulong value)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return TryGetUlong_macos(name, out value);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return TryGetUlong_linux(name, out value);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return TryGetUlong_windows(name, out value);

        value = 0;
        return false;
    }

    // 各 OS 檔案提供這三個方法的實作（或 stub）
    internal static partial bool TryGetUlong_macos(string name, out ulong value);
    internal static partial bool TryGetUlong_linux(string name, out ulong value);
    internal static partial bool TryGetUlong_windows(string name, out ulong value);
}
