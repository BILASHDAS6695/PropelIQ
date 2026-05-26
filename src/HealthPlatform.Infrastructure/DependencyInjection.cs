using HealthPlatform.Application.Interfaces;
using HealthPlatform.Infrastructure.Cache;
using HealthPlatform.Infrastructure.Persistence;
using HealthPlatform.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace HealthPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
            options
                .UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(
                            typeof(ApplicationDbContext).Assembly.FullName);
                        npgsqlOptions.MaxBatchSize(100);
                    })
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(auditInterceptor);
        });

        var redisConfig = ConfigurationOptions.Parse(
            configuration["Redis:ConnectionString"] ?? "localhost:6379");
        redisConfig.Ssl                = bool.Parse(configuration["Redis:Ssl"] ?? "false");
        redisConfig.ConnectTimeout     = int.Parse(configuration["Redis:ConnectTimeout"] ?? "5000");
        redisConfig.SyncTimeout        = int.Parse(configuration["Redis:SyncTimeout"] ?? "1000");
        redisConfig.AbortOnConnectFail = bool.Parse(
            configuration["Redis:AbortOnConnectFail"] ?? "false");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConfig));

        services.AddScoped<ISessionStore, RedisSessionStore>();
        services.AddSingleton<ICacheService, RedisCacheService>();

        services.AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "postgres",
                tags: ["db", "ready"])
            .AddDbContextCheck<ApplicationDbContext>(
                name: "efcore",
                tags: ["db", "ready"])
            .AddRedis(
                configuration["Redis:ConnectionString"] ?? "localhost:6379",
                name: "redis",
                tags: ["cache", "ready"]);

        return services;
    }
}
