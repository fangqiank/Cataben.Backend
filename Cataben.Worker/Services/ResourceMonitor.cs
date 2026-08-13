using System.Diagnostics;

namespace Cataben.Worker.Services
{
    public class ResourceMonitor
    {
        private readonly ILogger<ResourceMonitor> _logger;
        private readonly Process _currentProcess;
        private DateTime _lastCpuCheck;
        private TimeSpan _lastCpuTime;

        public ResourceMonitor(ILogger<ResourceMonitor> logger)
        {
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
            _lastCpuCheck = DateTime.UtcNow;
            _lastCpuTime = _currentProcess.TotalProcessorTime;
        }

        public ResourceMetrics GetCurrentMetrics()
        {
            try
            {
                var memoryUsage = _currentProcess.WorkingSet64;
                var privateMemory = _currentProcess.PrivateMemorySize64;

                // Calculate CPU usage
                var now = DateTime.UtcNow;
                var cpuTime = _currentProcess.TotalProcessorTime;
                var cpuUsage = CalculateCpuUsage(cpuTime, now);

                // Update last values
                _lastCpuTime = cpuTime;
                _lastCpuCheck = now;

                return new ResourceMetrics
                {
                    CpuUsagePercent = cpuUsage,
                    MemoryUsageMb = memoryUsage / (1024.0 * 1024.0),
                    PrivateMemoryMb = privateMemory / (1024.0 * 1024.0),
                    ThreadCount = _currentProcess.Threads.Count,
                    HandleCount = _currentProcess.HandleCount,
                    Uptime = DateTime.UtcNow - _currentProcess.StartTime,
                    GcCollections = GC.CollectionCount(0)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting resource metrics");
                return new ResourceMetrics();
            }
        }

        private double CalculateCpuUsage(TimeSpan cpuTime, DateTime now)
        {
            try
            {
                var elapsed = now - _lastCpuCheck;
                var cpuUsed = cpuTime - _lastCpuTime;

                if (elapsed.TotalSeconds < 0.1)
                    return 0;

                var cpuUsage = (cpuUsed.TotalMilliseconds / elapsed.TotalMilliseconds) * 100;

                // Cap at 100% (per core)
                var processorCount = Environment.ProcessorCount;
                return Math.Min(processorCount * 100, cpuUsage);
            }
            catch
            {
                return 0;
            }
        }

        public bool IsMemoryExhausted(long thresholdMb = 400)
        {
            var metrics = GetCurrentMetrics();
            return metrics.MemoryUsageMb > thresholdMb;
        }

        public bool IsCpuOverloaded(double thresholdPercent = 80)
        {
            var metrics = GetCurrentMetrics();
            return metrics.CpuUsagePercent > thresholdPercent;
        }

        public void LogMetrics()
        {
            var metrics = GetCurrentMetrics();
            _logger.LogDebug(
                "Resource Metrics - CPU: {Cpu:F1}%, Memory: {Memory:F1}MB, Private: {Private:F1}MB, Threads: {Threads}, Handles: {Handles}, GC: {Gc}",
                metrics.CpuUsagePercent,
                metrics.MemoryUsageMb,
                metrics.PrivateMemoryMb,
                metrics.ThreadCount,
                metrics.HandleCount,
                metrics.GcCollections);
        }
    }

    public class ResourceMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMb { get; set; }
        public double PrivateMemoryMb { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public TimeSpan Uptime { get; set; }
        public int GcCollections { get; set; }
    }
}
