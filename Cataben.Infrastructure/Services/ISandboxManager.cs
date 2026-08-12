using System.Reflection;

namespace Cataben.Infrastructure.Services
{
    public interface ISandboxManager
    {
        Task<SandboxExecutionResult> ExecuteInSandboxAsync(Assembly assembly, Dictionary<string, object> parameters, ExecutionOptions options, CancellationToken cancellationToken);
    }
}