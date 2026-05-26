using HealthPlatform.Application.Interfaces;
using HealthPlatform.Infrastructure.Cache;
using HealthPlatform.Infrastructure.Persistence;
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
        services.AddDbContext<ApplicationDbContext>(options =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions => npgsqlOptions.MigrationsAssembly(
                        typeof(ApplicationDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention());

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
        services.AddScoped<IUnitOfWork, UnitOfWork>();

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
