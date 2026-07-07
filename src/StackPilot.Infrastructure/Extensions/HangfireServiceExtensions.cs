using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace StackPilot.Infrastructure.Extensions;

public static class HangfireServiceExtensions
{
    public static IServiceCollection AddStackPilotHangfire(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        bool runServer)
    {
        if (environment.IsEnvironment("Testing"))
            return services;

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=stackpilot;Username=stackpilot;Password=stackpilot";

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

        if (runServer)
        {
            services.AddHangfireServer(options =>
            {
                options.Queues = ["critical", "default", "low"];
                options.WorkerCount = Math.Max(2, Environment.ProcessorCount);
            });
        }

        return services;
    }
}
