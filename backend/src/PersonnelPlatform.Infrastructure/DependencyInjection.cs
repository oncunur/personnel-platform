using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Infrastructure.Audit;
using PersonnelPlatform.Infrastructure.Health;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", DatabaseSchemas.System)));

        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<AuditService>();

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton<RedisTcpHealthCheck>();

        services
            .AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "postgres",
                tags: ["ready"])
            .AddCheck<RedisTcpHealthCheck>(
                name: "redis",
                tags: ["ready"]);

        return services;
    }
}
