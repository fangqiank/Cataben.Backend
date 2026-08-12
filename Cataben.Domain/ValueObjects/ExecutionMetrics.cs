namespace Cataben.Domain.ValueObjects
{
    public class ExecutionMetrics
    {
        public long ExecutionTimeMs { get; set; }
        public long MemoryAllocatedBytes { get; set; }
        public long CpuTimeMs { get; set; }
        public int ThreadCount { get; set; }
        public int GcCollections { get; set; }
        public long PeakMemoryUsage { get; set; }
        public string? QueryPlan { get; set; }
        public int QueryCost { get; set; }
    }
}
