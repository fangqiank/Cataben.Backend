using Cataben.Application.DTOs;
using Cataben.Domain.Enums;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;

namespace Cataben.Infrastructure.Services
{

    public class CodeExecutorService(
        ISandboxManager sandboxManager,
        ILogger<CodeExecutorService> logger)
        : ICodeExecutor
    {
        private readonly List<PortableExecutableReference> _references = new()
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Data.DataTable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Net.WebClient).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
        };

        public async Task<ExecutionResultDto> ExecuteAsync(
            string code,
            Dictionary<string, object> parameters,
            ExecutionOptions options,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ExecutionResultDto();

            try
            {
                // 1. Compile code
                var compilationResult = await CompileCodeAsync(code, cancellationToken);
                if (!compilationResult.Success)
                {
                    result.IsSuccessful = false;
                    result.Error = string.Join("\n", compilationResult.Errors);
                    result.Status = SubmissionStatus.Failed;
                    return result;
                }

                // 2. Execute in sandbox
                var executionResult = await sandboxManager.ExecuteInSandboxAsync(
                    compilationResult.Assembly!,
                    parameters,
                    options,
                    cancellationToken);

                // 3. Map results
                result.IsSuccessful = executionResult.Success;
                result.Output = executionResult.Output;
                result.Error = executionResult.Error;
                result.ExecutionTimeMs = executionResult.ExecutionTimeMs;
                result.MemoryAllocatedBytes = executionResult.MemoryAllocatedBytes;
                result.QueryPlan = executionResult.QueryPlan;
                result.Status = executionResult.Success ? SubmissionStatus.Completed : SubmissionStatus.Failed;

                stopwatch.Stop();
                logger.LogInformation("Code executed in {ExecutionTime}ms", result.ExecutionTimeMs);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Code execution failed");
                result.IsSuccessful = false;
                result.Error = ex.Message;
                result.Status = SubmissionStatus.SystemError;
                return result;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private async Task<CompilationResult> CompileCodeAsync(string code, CancellationToken cancellationToken)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                code,
                new CSharpParseOptions(LanguageVersion.Latest),
                cancellationToken: cancellationToken);

            var compilationOptions = new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable,
                usings: new[] { "System", "System.Linq", "System.Collections.Generic" });

            var compilation = CSharpCompilation.Create(
                $"Sandbox_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                _references,
                compilationOptions);

            using var memoryStream = new MemoryStream();
            var emitResult = compilation.Emit(memoryStream, cancellationToken: cancellationToken);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage());
                return CompilationResult.Failed(errors);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(memoryStream.ToArray());

            return CompilationResult.Succeeded(assembly);
        }

        private record CompilationResult
        {
            public bool Success { get; init; }
            public Assembly? Assembly { get; init; }
            public IEnumerable<string> Errors { get; init; } = [];

            public static CompilationResult Succeeded(Assembly assembly) => new()
            {
                Success = true,
                Assembly = assembly
            };

            public static CompilationResult Failed(IEnumerable<string> errors) => new()
            {
                Success = false,
                Errors = errors
            };
        }

    }
}
