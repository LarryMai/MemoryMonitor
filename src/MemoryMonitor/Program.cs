using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using MemoryMonitor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

public static class Program
{
    private static SystemMemory _lastSys;
    private static ProcessMemory _lastProc;
    private static CpuSnapshot _lastCpu;

    public static async Task Main(string[] args)
    {
        var (intervalSec, metricsEndpoint) = ParseArgs(args);
        Console.WriteLine($"[Monitor] Sampling every {intervalSec}s (Windows/Linux/macOS). Press Ctrl+C to stop.");
        if (metricsEndpoint is not null)
            Console.WriteLine($"[Monitor] Prometheus /metrics on http://{metricsEndpoint}/metrics\n");
        else
            Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // Start metrics server if requested
        Task? metricsTask = null;
        if (metricsEndpoint is not null)
            metricsTask = RunMetricsServerAsync(metricsEndpoint, cts.Token);

        // Initialize CPU sampler
        var cpu = new CpuSampler();
        cpu.TryPrime(); // take first baseline

        // Sampling loop
        try
        {
            while (!cts.IsCancellationRequested)
            {
                _lastSys = SystemMemory.Read();
                _lastProc = ProcessMemory.ReadCurrent();
                _lastCpu = cpu.Sample(); // compute usage since last call

                Console.WriteLine($"UTC: {DateTime.UtcNow:O}");
                Console.WriteLine($"System  Total={FormatBytes(_lastSys.TotalBytes)}  Used={FormatBytes(_lastSys.UsedBytes)}  Avail={FormatBytes(_lastSys.AvailableBytes)}  Used%={_lastSys.UsedPercent:F1}%");
                Console.WriteLine($"CPU     Overall={_lastCpu.OverallUsageRatio:P1}  LogicalCores={_lastCpu.LogicalProcessorCount}  PerCore={(_lastCpu.PerCoreUsageRatios?.Length ?? 0)} entries");
                Console.WriteLine($"Process WS={FormatBytes(_lastProc.WorkingSetBytes)}  Paged={FormatBytes(_lastProc.PagedBytes)}  Private={FormatBytes(_lastProc.PrivateBytes)}");
                Console.WriteLine(new string('-', 100));

                await Task.Delay(TimeSpan.FromSeconds(intervalSec), cts.Token);
            }
        }
        catch (TaskCanceledException) { /* normal exit */ }

        if (metricsTask is not null) await metricsTask;
    }

    private static (int intervalSec, string? metricsEndpoint) ParseArgs(string[] args)
    {
        int intervalSec = 1; string? metrics = null;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (int.TryParse(a, out var s) && s > 0) { intervalSec = s; continue; }
            if (a == "--metrics" && i + 1 < args.Length) { metrics = args[++i]; continue; }
        }
        return (intervalSec, metrics);
    }

    private static async Task RunMetricsServerAsync(string endpoint, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            ApplicationName = typeof(Program).Assembly.FullName,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.UseKestrel(opts =>
        {
            var parts = endpoint.Split(':', 2);
            var host = parts[0];
            var port = int.Parse(parts[1]);
            opts.Listen(System.Net.IPAddress.Parse(host), port);
        });

        var app = builder.Build();

        // Home
        app.MapGet("/", () => Results.Text("Memory & CPU Monitor API Server (/.NET 8)\n/metrics, /healthz, /api/v1/memory/system, /api/v1/memory/process, /api/v1/cpu\n"));

        // Health
        app.MapGet("/healthz", () => Results.Json(new { status = "ok", utc = DateTime.UtcNow }));

        // System memory
        app.MapGet("/api/v1/memory/system", () => Results.Json(_lastSys));

        // Process list (top 10 by WorkingSet)
        app.MapGet("/api/v1/memory/process", () => Results.Json(ProcessLister.TopByWorkingSet(10)));

        // CPU (overall + per-core if available)
        app.MapGet("/api/v1/cpu", () => Results.Json(_lastCpu));

        // Prometheus metrics
        app.MapGet("/metrics", (HttpContext ctx) =>
        {
            var sys = _lastSys; var proc = _lastProc; var cpu = _lastCpu;
            var sb = new System.Text.StringBuilder();
            // memory
            sb.AppendLine($"system_memory_total_bytes {sys.TotalBytes}");
            sb.AppendLine($"system_memory_available_bytes {sys.AvailableBytes}");
            sb.AppendLine($"system_memory_used_bytes {sys.UsedBytes}");
            sb.AppendLine($"system_memory_used_ratio {sys.UsedPercent / 100.0:0.####}");
            sb.AppendLine($"process_working_set_bytes{{pid=\"{proc.ProcessId}\"}} {proc.WorkingSetBytes}");
            if (proc.PrivateBytes != 0) sb.AppendLine($"process_private_bytes{{pid=\"{proc.ProcessId}\"}} {proc.PrivateBytes}");
            if (proc.PagedBytes != 0) sb.AppendLine($"process_paged_bytes{{pid=\"{proc.ProcessId}\"}} {proc.PagedBytes}");
            // cpu overall
            sb.AppendLine($"system_cpu_usage_ratio {cpu.OverallUsageRatio:0.####}");
            sb.AppendLine($"system_cpu_logical_processors {cpu.LogicalProcessorCount}");
            // cpu per-core if present
            if (cpu.PerCoreUsageRatios is not null)
            {
                for (int i = 0; i < cpu.PerCoreUsageRatios.Length; i++)
                {
                    sb.AppendLine($"system_cpu_core_usage_ratio{{core=\"{i}\"}} {cpu.PerCoreUsageRatios[i]:0.####}");
                }
            }
            return Results.Text(sb.ToString(), "text/plain; version=0.0.4");
        });

        await app.RunAsync(ct);
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"]; double v = bytes; int i = 0; while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {units[i]}";
    }
}