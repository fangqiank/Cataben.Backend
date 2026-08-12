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

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    // Load user secrets AFTER appsettings so they override placeholder values
    // (e.g. Clerk:Issuer). CreateBuilder already adds them in Development, but the
    // explicit AddJsonFile calls above are appended later and would otherwise win.
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

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
builder.Services.AddSingleton<IMessageBus, NatsMessageBus>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddScoped<IQuestService, QuestService>();
builder.Services.AddScoped<IRewardService, RewardService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICodeExecutor, CodeExecutorService>();
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

await app.RunAsync();
