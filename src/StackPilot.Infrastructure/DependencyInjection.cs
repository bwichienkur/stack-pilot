using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.AI;
using StackPilot.Application.Common;
using StackPilot.Application.Connectors;
using StackPilot.Application.Interfaces;
using StackPilot.Infrastructure.Connectors;
using StackPilot.Infrastructure.Jobs;
using StackPilot.Infrastructure.Persistence;
using StackPilot.Infrastructure.Security;
using StackPilot.Infrastructure.Services;

namespace StackPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantContext, TenantContext>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=stackpilot;Username=stackpilot;Password=stackpilot"));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IConnectorService, ConnectorService>();
        services.AddScoped<IGraphService, GraphService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IDocumentationService, DocumentationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IIntelligenceService, IntelligenceService>();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<IAiGovernanceService, AiGovernanceService>();
        services.AddSingleton<ICredentialEncryptionService, CredentialEncryptionService>();
        services.AddSingleton<IBackgroundJobService, BackgroundJobService>();

        services.AddSingleton<IAiProvider, MockAiProvider>();
        services.AddSingleton<IConnector, GitHubRepositoryConnector>();
        services.AddSingleton<IConnector, GitHubActionsConnector>();
        services.AddSingleton<IConnector, SqlServerConnector>();
        services.AddSingleton<IConnector, PostgreSQLConnector>();
        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
        services.AddSingleton<IRepositoryScanner, RepositoryScanner>();
        services.AddSingleton<IDatabaseScanner, DatabaseScanner>();

        services.AddScoped<ConnectorSyncJob>();
        services.AddScoped<RepositoryScanJob>();
        services.AddScoped<DatabaseScanJob>();
        services.AddScoped<GenerateRequirementsJob>();

        return services;
    }
}
