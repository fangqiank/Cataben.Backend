using Cataben.Application.Services;
using Cataben.Shared.Execution;
using Cataben.Shared.Constants;
using Cataben.Shared.Messaging;
using Cataben.Worker.HealthChecks;
using Cataben.Worker.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NATS.Extensions.Microsoft.DependencyInjection;
using System.Net.Http.Headers;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    // Explicit (CreateApplicationBuilder already adds this in Development) so Ai:ApiKey resolves
    // regardless of environment. Like the API's Clerk:Issuer, secrets never live in committed json.
    .AddUserSecrets<Program>(optional: true);

// Real NATS. Worker and Infrastructure are sibling outer layers (neither references the other —
// the old Infrastructure→Worker ProjectReference was dead and has been removed), so they share the
// NATS-backed IMessageBus from Cataben.Shared. AddNatsClient registers INatsConnection (singleton)
// and binds the "Nats" config section for the URL.
var natsUrl = builder.Configuration["Nats:Url"] ?? "nats://localhost:4222";
builder.Services.AddNatsClient(nats => nats.ConfigureOptions(opts =>
    opts.Configure(o => o.Opts = o.Opts with { Url = natsUrl })));
builder.Services.AddSingleton<IMessageBus, NatsMessageBus>();

// Fail loudly on an unconfigured result-signing key: it HMAC-signs every execution result
// published on code.result.{id}, and the API verifies with the SAME key. The value committed
// in appsettings.json is a placeholder known to anyone who reads the repo, so production must
// override it — a mismatch (or the placeholder) means the API silently drops every result.
var resultSigningKey = builder.Configuration["Nats:ResultSigningKey"];
if (string.IsNullOrWhiteSpace(resultSigningKey) ||
    resultSigningKey.Equals(ExecutionResultSigner.PlaceholderKey, StringComparison.Ordinal))
{
    var remediation =
        "Nats:ResultSigningKey is not configured (missing or still the placeholder). " +
        "Every execution result this Worker publishes is HMAC-signed with it and the API verifies with the same key. " +
        @"Fix:  dotnet user-secrets set ""Nats:ResultSigningKey"" ""<random secret>"" --project Cataben.Worker  (the SAME value must be set on Cataben.API)";
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException(remediation);
    Console.WriteLine($"[WARN] {remediation}  (continuing in Development — the API must use the SAME key or it drops this Worker's results)");
}

// Stateless execution primitives, consumed by the singleton ExecutionWorker hosted service →
// must be singletons themselves (a singleton cannot consume scoped services; the strict DI
// validator enforced in Development rejects it). None hold per-request or DbContext state:
// CodeCompiler's references/usings are fixed at construction, SandboxExecutor/TestRunner are
// pure, and ResourceMonitor's mutable fields are written only by the single monitor loop.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ProcessCodeRunner>();
builder.Services.AddSingleton<CodeCompiler>();
builder.Services.AddSingleton<SandboxExecutor>();
builder.Services.AddSingleton<TestRunner>();
builder.Services.AddSingleton<ResourceMonitor>();

// Distributed cache backs AiJudgeService's verdict cache (identical (expected,actual,hint) → same
// verdict, since the judge runs at temperature 0). Memory-backed here; cross-replica sharing would
// need Redis (not wired for the Worker yet).
builder.Services.AddDistributedMemoryCache();

// AI semantic judging (DeepSeek via its OpenAI-compatible REST endpoint, called with a raw
// HttpClient — see AiJudgeService remarks for why not the OpenAI SDK: the SDK sends
// max_completion_tokens, which DeepSeek ignores, letting the reasoning model generate unbounded).
// Optional: with no key, AI-mode test cases fail soft (NoOpAiJudgeService) instead of crashing the
// Worker or poisoning the NATS queue. Ai:ApiKey comes from user-secrets, never committed json.
var aiConfig = builder.Configuration.GetSection("Ai");
var aiApiKey = aiConfig["ApiKey"];
if (!string.IsNullOrWhiteSpace(aiApiKey))
{
    // Named HttpClient: BaseAddress (trailing '/' so the relative "v1/chat/completions" request
    // resolves correctly) + Bearer auth set once here. AiJudgeService is a singleton consumed by
    // the singleton TestRunner, so it resolves the client per-call via IHttpClientFactory — the
    // DI-safe pattern for a singleton to use HttpClients (avoids the captive-dependency the strict
    // Development validator rejects). Timeout is InfiniteTimeSpan: AiJudgeService bounds the call
    // itself via its own CTS (Ai:TimeoutSeconds); a second HttpClient.Timeout would double-cut it.
    var aiBaseUri = (aiConfig["BaseUri"] ?? "https://api.deepseek.com").TrimEnd('/') + "/";
    builder.Services.AddHttpClient(AiJudgeService.AiClientName, c =>
    {
        c.BaseAddress = new Uri(aiBaseUri);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiApiKey);
        c.Timeout = Timeout.InfiniteTimeSpan;
    });
    builder.Services.AddSingleton<IAiJudgeService, AiJudgeService>();
}
else
{
    // Dev-friendly: a missing key keeps the Worker up. AI cases just report "not configured".
    builder.Services.AddSingleton<IAiJudgeService, NoOpAiJudgeService>();
}

builder.Services.AddHostedService<ExecutionWorker>();

builder.Services.AddHealthChecks()
    .AddCheck("NATS", () => HealthCheckResult.Healthy())
    .AddCheck("Memory", () =>
    {
        var memory = GC.GetTotalMemory(false);
        var memoryLimit = 512 * 1024 * 1024; // 512MB
        return memory < memoryLimit
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded($"Memory usage high: {memory / 1024 / 1024}MB");
    })
    .AddCheck<SandboxHealthCheck>("Sandbox");

var host = builder.Build();

// Surface which AI judge registered — confirms Ai:ApiKey was read (DeepSeek) vs fail-soft NoOp.
// NoOp means ai-validated cases always report Passed=false without ever calling DeepSeek.
host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup")
    .LogInformation("AI judge mode: {Mode}",
        string.IsNullOrWhiteSpace(aiApiKey) ? "NoOp (Ai:ApiKey not set)" : "DeepSeek (AiJudgeService)");

// Provision the JetStream stream backing code.execute before the durable consumer attaches.
// Fail-fast: if NATS is unreachable the Worker cannot do its job, so surface it at startup.
using (var scope = host.Services.CreateScope())
{
    var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
    await bus.EnsureStreamAsync(
        ApplicationConstants.Nats.ExecutionsStream,
        new[] { ApplicationConstants.Nats.ExecutionsStreamSubject },
        CancellationToken.None);
    await bus.EnsureStreamAsync(
        ApplicationConstants.Nats.ResultsStream,
        new[] { ApplicationConstants.Nats.ResultsStreamSubject },
        CancellationToken.None);
}

await host.RunAsync();
