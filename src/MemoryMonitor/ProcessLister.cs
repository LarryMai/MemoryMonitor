using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemoryMonitor
{
    public static class ProcessLister
    {
        public sealed record ProcItem(int Pid, string Name, ulong WorkingSetBytes);

        public static IReadOnlyList<ProcItem> TopByWorkingSet(int take)
        {
            var list = new List<ProcItem>(128);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    list.Add(new ProcItem(p.Id, p.ProcessName, (ulong)p.WorkingSet64));
                }
                catch { /* some system processes may deny access */ }
                finally { try { p.Dispose(); } catch { } }
            }
            return list.OrderByDescending(x => x.WorkingSetBytes).Take(take).ToArray();
        }
    }
}
