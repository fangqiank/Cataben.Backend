using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Text;

namespace Cataben.Worker.Services
{
    public class CodeCompiler
    {
        private readonly ILogger<CodeCompiler> _logger;
        private readonly List<PortableExecutableReference> _references;
        private readonly List<string> _defaultUsings;

        public CodeCompiler(ILogger<CodeCompiler> logger)
        {
            _logger = logger;
            _references = [];
            _defaultUsings =
            [
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "System.Text",
                "System.Threading.Tasks"
            ];

            // Add common references
            AddReference(typeof(object));
            AddReference(typeof(Console));
            AddReference(typeof(Enumerable));
            AddReference(typeof(System.Data.DataTable));
            AddReference(typeof(System.Text.Json.JsonSerializer));
            AddReference(typeof(System.Net.WebClient));
            AddReference(typeof(System.Threading.Tasks.Task));
            AddReference(typeof(System.Collections.Generic.List<>));
            AddReference(typeof(System.Collections.Generic.Dictionary<,>));

            // Roslyn resolves primitive types (object/string/int/...) against the System.Runtime
            // contract identity, but typeof(object).Assembly is System.Private.CoreLib (the
            // *implementation*). Referencing only CoreLib fails with "type 'Object' is defined in
            // an assembly that is not referenced: System.Runtime". The System.Runtime facade
            // (always present at runtime) type-forwards those primitives to CoreLib — referencing
            // it alongside CoreLib gives Roslyn the contract identity it needs to resolve them.
            AddFacadeReference("System.Runtime");
        }

        private void AddReference(Type type)
        {
            try
            {
                var location = type.Assembly.Location;
                if (!string.IsNullOrEmpty(location) && File.Exists(location))
                {
                    _references.Add(MetadataReference.CreateFromFile(location));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add reference for {Type}", type.FullName);
            }
        }

        private void AddFacadeReference(string assemblyName)
        {
            try
            {
                // Assembly.Load forces the facade (always loadable in the runtime) into the load
                // context so its on-disk Location is available.
                var facade = Assembly.Load(assemblyName);
                if (!string.IsNullOrEmpty(facade.Location) && File.Exists(facade.Location))
                {
                    _references.Add(MetadataReference.CreateFromFile(facade.Location));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add facade reference {Assembly}", assemblyName);
            }
        }

        public async Task<CompilationResult> CompileAsync(string code, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Compiling code...");

                // Build full code with using statements
                var fullCode = BuildFullCode(code);

                var syntaxTree = CSharpSyntaxTree.ParseText(
                    fullCode,
                    new CSharpParseOptions(LanguageVersion.Latest),
                    cancellationToken: cancellationToken);

                var compilationOptions = new CSharpCompilationOptions(
                    OutputKind.ConsoleApplication,
                    optimizationLevel: OptimizationLevel.Release,
                    allowUnsafe: false,
                    nullableContextOptions: NullableContextOptions.Enable,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
                    concurrentBuild: true,
                    usings: _defaultUsings);

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
                        .Select(d => d.GetMessage())
                        .ToList();

                    _logger.LogWarning("Compilation failed with {ErrorCount} errors", errors.Count);
                    return CompilationResult.Failure(errors);
                }

                memoryStream.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(memoryStream.ToArray());
                var assemblyBytes = memoryStream.ToArray();

                _logger.LogDebug("Compilation successful");
                return CompilationResult.Succeeded(assembly, assemblyBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Compilation failed");
                return CompilationResult.Failure(new[] { ex.Message });
            }
        }

        private string BuildFullCode(string userCode)
        {
            var sb = new StringBuilder();

            // Add using statements
            foreach (var usingStatement in _defaultUsings)
            {
                sb.AppendLine($"using {usingStatement};");
            }

            sb.AppendLine();

            // Add user code
            sb.AppendLine(userCode);

            if (!HasEntryPoint(userCode))
            {
                sb.AppendLine(@"
                    class CatabenSandboxProgram {
                        static void Main() {
                            try {
                                // User's code should provide a method called Execute
                                Execute();
                            } catch (Exception ex) {
                                Console.WriteLine($""ERROR: {ex.Message}"");
                                Console.WriteLine(ex.StackTrace);
                            }
                        }
                    }");
            }

            return sb.ToString();
        }

        private static bool HasEntryPoint(string userCode)
        {
            var tree = CSharpSyntaxTree.ParseText(
                userCode,
                new CSharpParseOptions(LanguageVersion.Latest));
            var root = tree.GetRoot();

            if (root.ChildNodes().OfType<GlobalStatementSyntax>().Any())
                return true;

            return root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.ValueText == "Main"
                    && m.Modifiers.Any(SyntaxKind.StaticKeyword));
        }
    }

    public class CompilationResult
    {
        public bool Success { get; private set; }
        public Assembly? Assembly { get; private set; }
        public byte[]? AssemblyBytes { get; private set; }
        public IEnumerable<string>? Errors { get; private set; }

        public static CompilationResult Succeeded(Assembly assembly, byte[] assemblyBytes)
        {
            return new CompilationResult
            {
                Success = true,
                Assembly = assembly,
                AssemblyBytes = assemblyBytes
            };
        }

        public static CompilationResult Failure(IEnumerable<string> errors)
        {
            return new CompilationResult
            {
                Success = false,
                Errors = errors
            };
        }
    }

}
