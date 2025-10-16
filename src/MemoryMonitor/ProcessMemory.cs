using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public readonly record struct ProcessMemory(
    int ProcessId,
    ulong WorkingSetBytes,
    ulong PrivateBytes,
    ulong PagedBytes)
{
    public static ProcessMemory ReadCurrent()
    {
        using var p = Process.GetCurrentProcess();
        ulong ws = (ulong)p.WorkingSet64;
        ulong priv = 0, paged = 0;
        try { priv = (ulong)p.PrivateMemorySize64; } catch { }
        try { paged = (ulong)p.PagedMemorySize64; } catch { }
        return new(p.Id, ws, priv, paged);
    }
}
