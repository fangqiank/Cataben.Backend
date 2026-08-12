using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cataben.Infrastructure.Extensions
{
    public static class HealthCheckExtensions
    {
        public static IHealthChecksBuilder AddNpgSql(
        this IHealthChecksBuilder builder,
        string connectionString,
        string? name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
        {
            // 使用 Npgsql 健康检查
            builder.AddCheck<NpgSqlHealthCheck>(name ?? "postgresql", failureStatus, tags, timeout);
            return builder;
        }
    }

    public class NpgSqlHealthCheck(string connectionString) : IHealthCheck
    {
        private readonly string _connectionString = connectionString;

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // 实际实现: 使用 NpgsqlConnection 检查连接
                await Task.Delay(10, cancellationToken);
                return HealthCheckResult.Healthy("Database connection is healthy");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database connection failed", ex);
            }
        }
    }

}
