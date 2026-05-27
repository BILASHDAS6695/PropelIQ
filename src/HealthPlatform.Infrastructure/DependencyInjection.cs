using Hangfire;
using Hangfire.PostgreSql;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using HealthPlatform.Infrastructure.Cache;
using HealthPlatform.Infrastructure.Jobs;
using HealthPlatform.Infrastructure.Messaging;
using HealthPlatform.Infrastructure.Persistence;
using HealthPlatform.Infrastructure.Persistence.Interceptors;
using HealthPlatform.Infrastructure.Persistence.Seed;
using HealthPlatform.Infrastructure.Security;
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
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions => npgsqlOptions.MigrationsAssembly(
                        typeof(ApplicationDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        var redisConfig = ConfigurationOptions.Parse(
            configuration["Redis:ConnectionString"] ?? "localhost:6379");
        redisConfig.Ssl                = bool.Parse(configuration["Redis:Ssl"] ?? "false");
        redisConfig.ConnectTimeout     = int.Parse(configuration["Redis:ConnectTimeout"] ?? "5000");
        redisConfig.SyncTimeout        = int.Parse(configuration["Redis:SyncTimeout"] ?? "1000");
        redisConfig.AbortOnConnectFail = bool.Parse(
            configuration["Redis:AbortOnConnectFail"] ?? "false");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConfig));

        var useInMemorySessionStore = bool.Parse(
            configuration["Redis:UseInMemorySessionStore"] ?? "false");

        if (useInMemorySessionStore)
            services.AddSingleton<ISessionStore, InMemorySessionStore>();
        else
            services.AddScoped<ISessionStore, RedisSessionStore>();

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailSender, NoOpEmailSender>();
        services.AddHostedService<SlotGenerationService>();
        services.AddHostedService<SwapRequestExpiryService>();

        services.AddHangfire(config =>
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(c =>
                    c.UseNpgsqlConnection(
                        configuration.GetConnectionString("DefaultConnection")!)));

        services.AddTransient<NoShowAutoMarkJob>();

        services.Configure<AccountSecuritySettings>(
            configuration.GetSection(AccountSecuritySettings.SectionName));

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
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tags: ["cache", "ready"]);

        return services;
    }
}
