namespace Cataben.Infrastructure.Services
{
    public interface ISandboxManager
    {
        Task<SandboxExecutionResult> ExecuteInSandboxAsync(
            byte[] assemblyBytes,
            Dictionary<string, object> parameters,
            ExecutionOptions options,
            CancellationToken cancellationToken);
    }
}
