using System.Net;
using System.Net.Sockets;
using API.ServiceFactory;
using API.Settings;
using Newtonsoft.Json;
using WatsonWebserver.Core;

namespace WebAPI.Routes;

public static class SystemRoutes
{
    /// <summary>Returns system/environment info for the dashboard (database provider, data source, database names). No secrets.</summary>
    public static async Task GetSystemInfo(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx))
        {
            await ctx.Response.Send("{\"error\":\"Too many requests\"}");
            return;
        }

        try
        {
            string? databaseProvider = null;
            string? dataSource = null;
            object? databases = null;

            try
            {
                var settingsManager = ServiceFactory.Load<ISettingsManager>(typeof(ISettingsManager));
                var settings = settingsManager?.Settings;
                if (settings != null)
                {
                    databaseProvider = "SqlServer";
                    dataSource = $"{settings.Address ?? ""},{settings.Port ?? 0}";
                    databases = new
                    {
                        proxy = settings.ProxyDb ?? "",
                        account = settings.AccountDb ?? "",
                        shard = settings.SharDb ?? "",
                        log = settings.LogDb ?? ""
                    };
                }
            }
            catch
            {
                // ISettingsManager may not be available
            }

            var result = new
            {
                databaseProvider,
                dataSource,
                databases
            };

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(result));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task GetMetrics(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx))
        {
            await ctx.Response.Send("{\"error\":\"Too many requests\"}");
            return;
        }

        try
        {
            var processCpuPercent = SystemMetrics.GetProcessCpuUsagePercent();
            double systemCpuTotalPercent = 0;
            double[] systemCpuPerCorePercent = System.Array.Empty<double>();
            string? metricsError = null;
            string[]? missingDlls = null;
            try
            {
                var systemCpu = SystemMetrics.GetSystemCpuUsage();
                systemCpuTotalPercent = systemCpu.totalPercent;
                systemCpuPerCorePercent = systemCpu.perCorePercent ?? System.Array.Empty<double>();
            }
            catch (Exception ex)
            {
                var msg = ex.Message ?? "";
                var inner = ex.InnerException?.Message ?? "";
                var combined = msg + " " + inner;
                // "Performance Counters are not supported on this platform" = DLL missing or not loaded in plugin context
                var isPerformanceCounterRelated = combined.IndexOf("Could not load file or assembly", StringComparison.OrdinalIgnoreCase) >= 0
                    || combined.IndexOf("PerformanceCounter", StringComparison.OrdinalIgnoreCase) >= 0
                    || combined.IndexOf("Performance Counters are not supported", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isPerformanceCounterRelated)
                {
                    var suggested = TryGetMissingAssemblyNames(combined);
                    if (suggested.Count > 0)
                        missingDlls = suggested.ToArray();
                    else
                        missingDlls = new[] { "runtimes/win/lib/net8.0/System.Diagnostics.PerformanceCounter.dll", "Copy entire build output including runtimes/ folder" };
                    metricsError = "System CPU metrics could not be loaded. Copy the full plugin build output into the plugin folder (e.g. plugins/ExampleWebPlugin/), including the runtimes folder. On Windows the implementation is in runtimes/win/lib/net8.0/System.Diagnostics.PerformanceCounter.dll — copy the entire build output, not just the root DLLs.";
                }
                else
                {
                    metricsError = "System CPU metrics unavailable: " + (ex.Message ?? "unknown error");
                }
            }
            var memoryUsedBytes = SystemMetrics.GetMemoryUsedBytes();
            var memoryTotalBytes = SystemMetrics.GetTotalPhysicalMemoryBytes();
            var processName = SystemMetrics.GetProcessName();

            string? hostName = null;
            string[]? localAddresses = null;
            try
            {
                hostName = Dns.GetHostName();
                var ips = Dns.GetHostEntry(hostName).AddressList
                    ?.Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.ToString())
                    .ToArray();
                if (ips != null && ips.Length > 0)
                    localAddresses = ips;
            }
            catch { /* ignore */ }

            var result = new
            {
                processCpuPercent,
                systemCpuTotalPercent,
                systemCpuPerCorePercent,
                memoryUsedBytes,
                memoryTotalBytes = memoryTotalBytes > 0 ? memoryTotalBytes : (long?)null,
                processName,
                hostName,
                localAddresses,
                metricsError,
                missingDlls
            };

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(result));
        }
        catch (System.Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    /// <summary>Extract assembly names from "Could not load file or assembly 'Name, Version=...'" style messages.</summary>
    private static System.Collections.Generic.List<string> TryGetMissingAssemblyNames(string message)
    {
        var list = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(message)) return list;
        var idx = 0;
        while (true)
        {
            var start = message.IndexOf("'", idx, StringComparison.Ordinal);
            if (start < 0) break;
            var end = message.IndexOf("'", start + 1, StringComparison.Ordinal);
            if (end < 0) break;
            var name = message.Substring(start + 1, end - start - 1).Trim();
            var comma = name.IndexOf(',');
            if (comma > 0)
                name = name.Substring(0, comma).Trim();
            if (!string.IsNullOrEmpty(name) && !name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                name = name + ".dll";
            if (!string.IsNullOrEmpty(name) && !list.Contains(name, StringComparer.OrdinalIgnoreCase))
                list.Add(name);
            idx = end + 1;
        }
        return list;
    }
}
