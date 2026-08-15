using Cataben.API.Filters;
using Cataben.API.Middleware;
using Cataben.API.Services;
using Cataben.Infrastructure.Data;
using Cataben.Infrastructure.HealthChecks;
using Cataben.Infrastructure.Repositories;
using Cataben.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using StackExchange.Redis;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Cataben.API.BackgroundServices;
using Cataben.Application.Services;
using Cataben.Shared.Constants;
using Cataben.Shared.Execution;
using Cataben.Shared.Messaging;
using NATS.Extensions.Microsoft.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    // Load user secrets AFTER appsettings so they override placeholder values
    // (e.g. Clerk:Issuer). CreateBuilder already adds them in Development, but the
    // explicit AddJsonFile calls above are appended later and would otherwise win.
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

// Fail loudly on an unconfigured Clerk webhook secret BEFORE serving traffic. The Clerk
// webhook (user.created) is the only thing that provisions User rows, so a missing secret
// would silently 401 every webhook — new signups would get no User row and see 404s
// platform-wide. appsettings.json ships the placeholder; the real whsec_... value belongs
// in user-secrets or an env var (see docs/clerk-local-testing.md).
var clerkWebhookSecret = builder.Configuration["Clerk:WebhookSecret"];
if (string.IsNullOrWhiteSpace(clerkWebhookSecret) ||
    clerkWebhookSecret.Equals(ClerkWebhookVerifier.PlaceholderSecret, StringComparison.OrdinalIgnoreCase))
{
    const string remediation =
        "Clerk:WebhookSecret is not configured (missing or still the placeholder). " +
        "Every Clerk webhook will be rejected with 401 and no User rows will be created for new signups. " +
        @"Fix:  dotnet user-secrets set ""Clerk:WebhookSecret"" ""whsec_..."" --project Cataben.API";
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException(remediation);
    Console.WriteLine($"[WARN] {remediation}  (continuing in Development — the webhook endpoint will 401 until it is set)");
}

// Same fail-loud treatment for the Worker→API result signing key: the Worker HMAC-signs
// every execution result and this host verifies it (ExecutionResultReceiver). The value
// committed in appsettings.json is a placeholder known to anyone who reads the repo, so a
// production deployment running on it accepts forged passing results (XP/gems/achievements)
// from anyone who can reach NATS.
var resultSigningKey = builder.Configuration["Nats:ResultSigningKey"];
if (string.IsNullOrWhiteSpace(resultSigningKey) ||
    resultSigningKey.Equals(ExecutionResultSigner.PlaceholderKey, StringComparison.Ordinal))
{
    const string remediation =
        "Nats:ResultSigningKey is not configured (missing or still the placeholder). " +
        "Execution results are HMAC-verified with this key; on the placeholder any NATS client can forge a passing result. " +
        @"Fix:  dotnet user-secrets set ""Nats:ResultSigningKey"" ""<random secret>"" --project Cataben.API  (the SAME value must be set on Cataben.Worker)";
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException(remediation);
    Console.WriteLine($"[WARN] {remediation}  (continuing in Development — the Worker must use the SAME key or its results are dropped)");
}

builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Cataben.API")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the JWT "sub" claim under its original name. Clerk puts the user id in "sub";
        // the default inbound mapping would rename it to ClaimTypes.NameIdentifier.
        options.MapInboundClaims = false;
        options.Authority = builder.Configuration["Clerk:Issuer"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Clerk:Issuer"]
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Default", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 10;
    });
    options.AddFixedWindowLimiter("Execution", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromSeconds(30);
        opt.QueueLimit = 5;
    });
    options.RejectionStatusCode = 429;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
    options.Filters.Add<PerformanceFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddCheck<SandboxHealthCheck>("Sandbox");

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(Program).Assembly,
        typeof(Cataben.Application.Handlers.ExecuteCodeHandler).Assembly
    );
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChallengeRepository, ChallengeRepository>();
builder.Services.AddScoped<ISubmissionRepository, SubmissionRepository>();
builder.Services.AddScoped<IAchievementRepository, AchievementRepository>();
builder.Services.AddScoped<IQuestRepository, QuestRepository>();
builder.Services.AddScoped<IXpTransactionRepository, XpTransactionRepository>();
builder.Services.AddScoped<IRewardRepository, RewardRepository>();
builder.Services.AddScoped<ILearningPathRepository, LearningPathRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddSingleton<ICacheService, RedisCacheService>();

// Real NATS: JetStream durable for code.execute + signed code.result.* delivery.
// AddNatsClient registers INatsConnection (singleton) bound to the "Nats" config section.
var natsUrl = builder.Configuration["Nats:Url"] ?? "nats://localhost:4222";
builder.Services.AddNatsClient(nats => nats.ConfigureOptions(opts =>
    opts.Configure(o => o.Opts = o.Opts with { Url = natsUrl })));
// IMessageBus/NatsMessageBus now come from Cataben.Shared.Messaging.
builder.Services.AddSingleton<IMessageBus, NatsMessageBus>();
// Consumes code.result.{id} (Core NATS) published by the Worker → persist outcome + gamify.
builder.Services.AddHostedService<ExecutionResultReceiver>();
builder.Services.AddScoped<ISubmissionCompletionService, SubmissionCompletionService>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddScoped<IQuestService, QuestService>();
builder.Services.AddScoped<IRewardService, RewardService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICodeExecutor, CodeExecutorService>();
builder.Services.AddSingleton<ProcessCodeRunner>();
builder.Services.AddScoped<ISandboxManager, SandboxManager>();
// Required by ExecuteCodeHandler / SubmitChallengeHandler (StartActivity). Was missing, which
// broke DI activation of those handlers at runtime (and design-time DbContext creation).
builder.Services.AddScoped<IDistributedTracing, OpenTelemetryService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
// Injects ClaimTypes.Role from User.Role so [CustomAuthorize(...)] can enforce role hierarchy.
// Auto-discovered by the framework — no other wiring needed.
builder.Services.AddScoped<IClaimsTransformation, RoleClaimsTransformation>();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Services.AddResponseCompression();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseResponseCompression();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Migrate + seed with a small retry to tolerate transient DB unavailability at startup.
    const int maxAttempts = 3;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            await SeedData.InitializeAsync(dbContext, builder.Configuration);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            Log.Warning(ex, "DB migrate/seed attempt {Attempt}/{Max} failed; retrying...", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
        }
    }
}

// Provision the JetStream stream backing code.execute (idempotent; the Worker does the same).
// Fail-fast: without NATS the submission pipeline cannot dispatch, so surface it at startup.
using (var natsScope = app.Services.CreateScope())
{
    var bus = natsScope.ServiceProvider.GetRequiredService<IMessageBus>();
    await bus.EnsureStreamAsync(
        ApplicationConstants.Nats.ExecutionsStream,
        new[] { ApplicationConstants.Nats.ExecutionsStreamSubject },
        CancellationToken.None);
    await bus.EnsureStreamAsync(
        ApplicationConstants.Nats.ResultsStream,
        new[] { ApplicationConstants.Nats.ResultsStreamSubject },
        CancellationToken.None);
}

await app.RunAsync();
