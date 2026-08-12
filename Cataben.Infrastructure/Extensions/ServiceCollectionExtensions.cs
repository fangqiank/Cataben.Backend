using Cataben.Infrastructure.HealthChecks;
using Cataben.Infrastructure.Repositories;
using Cataben.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cataben.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
        {
            // Repositories
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IChallengeRepository, ChallengeRepository>();
            services.AddScoped<ISubmissionRepository, SubmissionRepository>();
            services.AddScoped<IAchievementRepository, AchievementRepository>();
            services.AddScoped<ILearningPathRepository, LearningPathRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Services
            services.AddScoped<ICodeExecutor, CodeExecutorService>();
            services.AddScoped<ISandboxManager, SandboxManager>();
            services.AddScoped<IAchievementService, AchievementService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IDistributedTracing, OpenTelemetryService>();
            services.AddSingleton<IMessageBus, NatsMessageBus>();
            services.AddSingleton<ICacheService, RedisCacheService>();

            // Health Checks
            services.AddHealthChecks()
                .AddCheck<SandboxHealthCheck>("Sandbox");

            return services;
        }
    }
}
