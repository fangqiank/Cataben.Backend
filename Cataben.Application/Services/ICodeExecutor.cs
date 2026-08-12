using Cataben.Application.DTOs;

namespace Cataben.Application.Services
{
    public interface ICodeExecutor
    {
        Task<ExecutionResultDto> ExecuteAsync(
            string code,
            Dictionary<string, object> parameters,
            ExecutionOptions options,
            CancellationToken cancellationToken = default);
    }

    public class ExecutionOptions
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
        public long MaxMemoryBytes { get; set; } = 256 * 1024 * 1024;
        public bool EnableDatabase { get; set; }
        public bool CaptureQueryPlan { get; set; }
    }
}
