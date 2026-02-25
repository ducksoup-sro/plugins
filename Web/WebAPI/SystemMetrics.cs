using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WebAPI;

/// <summary>
/// Collects CPU (total + per-core) and memory metrics for the system and current process (proxy).
/// </summary>
public static class SystemMetrics
{
    private static readonly object CpuLock = new();
    private static DateTime _lastProcessCpuTime = DateTime.MinValue;
    private static TimeSpan _lastProcessorTime = TimeSpan.Zero;
    private static double _lastProcessCpuPercent;

    public static double GetProcessCpuUsagePercent()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var currentTime = DateTime.UtcNow;
            var currentProcessorTime = process.TotalProcessorTime;

            lock (CpuLock)
            {
                if (_lastProcessCpuTime != DateTime.MinValue)
                {
                    var timePassed = (currentTime - _lastProcessCpuTime).TotalMilliseconds;
                    if (timePassed >= 100)
                    {
                        var processorTimePassed = (currentProcessorTime - _lastProcessorTime).TotalMilliseconds;
                        var cpuCount = Environment.ProcessorCount;
                        _lastProcessCpuPercent = cpuCount > 0 && timePassed > 0
                            ? Math.Min(100, (processorTimePassed / (cpuCount * timePassed)) * 100)
                            : 0;
                    }
                }

                _lastProcessCpuTime = currentTime;
                _lastProcessorTime = currentProcessorTime;
                return Math.Round(_lastProcessCpuPercent, 2);
            }
        }
        catch
        {
            return 0;
        }
    }

    public static long GetMemoryUsedBytes()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    public static long GetTotalPhysicalMemoryBytes()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetTotalPhysicalMemoryLinux();
        try
        {
            return GetTotalPhysicalMemoryWindows();
        }
        catch
        {
            return 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    private static long GetTotalPhysicalMemoryWindows()
    {
        var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
            return 0;
        return (long)status.ullTotalPhys;
    }

    private static long GetTotalPhysicalMemoryLinux()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/meminfo");
            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                        return kb * 1024;
                    break;
                }
            }
        }
        catch { }
        return 0;
    }

    public static string GetProcessName()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.ProcessName ?? "DuckSoup";
        }
        catch
        {
            return "DuckSoup";
        }
    }

    /// <summary>
    /// Total system CPU (0-100) and per-core (array of 0-100). Refreshes every ~2s to avoid blocking.
    /// </summary>
    public static (double totalPercent, double[] perCorePercent) GetSystemCpuUsage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetSystemCpuUsageWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return GetSystemCpuUsageLinux();
        return (0, Array.Empty<double>());
    }

    [SupportedOSPlatform("windows")]
    private static (double totalPercent, double[] perCorePercent) GetSystemCpuUsageWindows()
    {
        try
        {
            const string categoryName = "Processor";
            const string counterName = "% Processor Time";
            using var totalCounter = new PerformanceCounter(categoryName, counterName, "_Total");
            totalCounter.NextValue();

            // Get per-core instance names dynamically (e.g. "0", "1", "2" or "0,0", "0,1" on some systems)
            var perCorePct = new List<double>();
            try
            {
                var category = new PerformanceCounterCategory(categoryName);
                var instances = category.GetInstanceNames()
                    .Where(n => !string.Equals(n, "_Total", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();
                var perCore = new List<PerformanceCounter>(instances.Length);
                foreach (var instance in instances)
                {
                    try
                    {
                        var counter = new PerformanceCounter(categoryName, counterName, instance);
                        counter.NextValue();
                        perCore.Add(counter);
                    }
                    catch
                    {
                        // Skip instance if counter creation fails (e.g. permission)
                    }
                }

                Thread.Sleep(200);
                var total = Math.Min(100, Math.Round(totalCounter.NextValue(), 2));
                foreach (var counter in perCore)
                {
                    try
                    {
                        perCorePct.Add(Math.Min(100, Math.Round(counter.NextValue(), 2)));
                    }
                    finally
                    {
                        counter.Dispose();
                    }
                }
                return (total, perCorePct.ToArray());
            }
            catch
            {
                // Fallback: total only, no per-core
                Thread.Sleep(200);
                var total = Math.Min(100, Math.Round(totalCounter.NextValue(), 2));
                return (total, Array.Empty<double>());
            }
        }
        catch (Exception)
        {
            // Rethrow so caller (SystemRoutes) can detect missing DLL and return metricsError/missingDlls
            throw;
        }
    }

    private static (long[] total, long[] idle) _lastLinuxSample;
    private static readonly object LinuxCpuLock = new();

    private static (double totalPercent, double[] perCorePercent) GetSystemCpuUsageLinux()
    {
        try
        {
            if (!File.Exists("/proc/stat"))
                return (0, Array.Empty<double>());

            var lines = File.ReadAllLines("/proc/stat");
            var cpuLines = lines.Where(l => l.StartsWith("cpu", StringComparison.Ordinal) && l.Length >= 4 && char.IsDigit(l[3])).ToArray();
            if (cpuLines.Length == 0)
                return (0, Array.Empty<double>());

            var total = new long[cpuLines.Length];
            var idle = new long[cpuLines.Length];
            for (var i = 0; i < cpuLines.Length; i++)
            {
                var parts = cpuLines[i].Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;
                if (!long.TryParse(parts[1], out var user)) continue;
                if (!long.TryParse(parts[2], out var nice)) continue;
                if (!long.TryParse(parts[3], out var system)) continue;
                if (!long.TryParse(parts[4], out var id)) continue;
                var iowait = parts.Length > 5 && long.TryParse(parts[5], out var iw) ? iw : 0L;
                var irq = parts.Length > 6 && long.TryParse(parts[6], out var ir) ? ir : 0L;
                var softirq = parts.Length > 7 && long.TryParse(parts[7], out var si) ? si : 0L;
                var steal = parts.Length > 8 && long.TryParse(parts[8], out var st) ? st : 0L;
                total[i] = user + nice + system + id + iowait + irq + softirq + steal;
                idle[i] = id + iowait;
            }

            lock (LinuxCpuLock)
            {
                if (_lastLinuxSample.total == null || _lastLinuxSample.total.Length != total.Length)
                {
                    _lastLinuxSample = (total.ToArray(), idle.ToArray());
                    return (0, new double[total.Length]);
                }

                var totalPercent = 0.0;
                var perCore = new double[total.Length];
                for (var i = 0; i < total.Length; i++)
                {
                    var totalDelta = total[i] - _lastLinuxSample.total[i];
                    var idleDelta = idle[i] - _lastLinuxSample.idle[i];
                    if (totalDelta > 0)
                        perCore[i] = Math.Min(100, Math.Round((1.0 - (double)idleDelta / totalDelta) * 100, 2));
                    totalPercent += perCore[i];
                }
                _lastLinuxSample = (total.ToArray(), idle.ToArray());
                var avgTotal = total.Length > 0 ? totalPercent / total.Length : 0;
                return (Math.Round(avgTotal, 2), perCore);
            }
        }
        catch
        {
            return (0, Array.Empty<double>());
        }
    }
}
