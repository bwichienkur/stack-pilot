using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using StackPilot.Application.AI;
using StackPilot.Application.Common;
using StackPilot.Application.Connectors;
using StackPilot.Application.Interfaces;
using StackPilot.Infrastructure.AI;
using StackPilot.Infrastructure.Caching;
using StackPilot.Infrastructure.Connectors;
using StackPilot.Infrastructure.External;
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
        services.AddMemoryCache();

        var redisConnection = configuration.GetConnectionString("Redis");
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(redisConnection) && environmentName != "Testing")
        {
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false;
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=stackpilot;Username=stackpilot;Password=stackpilot"));

        services.AddHttpClient<IGitHubApiClient, GitHubApiClient>();
        services.AddHttpClient<IGitLabApiClient, GitLabApiClient>();
        services.AddHttpClient<OpenAiProvider>();
        services.AddHttpClient<AnthropicProvider>();

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
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<INotificationService, SlackNotificationService>();
        services.AddHttpClient();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<IAiGovernanceService, AiGovernanceService>();
        services.AddScoped<IRagIndexService, RagIndexService>();
        services.AddScoped<IPermissionValidator, PermissionValidator>();
        services.AddScoped<IApprovalGateService, ApprovalGateService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddSingleton<ICredentialEncryptionService, CredentialEncryptionService>();
        services.AddSingleton<IBackgroundJobService, BackgroundJobService>();

        services.AddSingleton<MockAiProvider>();
        services.AddSingleton<OpenAiProvider>();
        services.AddSingleton<AnthropicProvider>();
        services.AddSingleton<IAiProvider, CompositeAiProvider>();

        services.AddSingleton<PostgreSqlDatabaseScanner>();
        services.AddSingleton<SqlServerDatabaseScanner>();
        services.AddSingleton<MySqlDatabaseScanner>();
        services.AddSingleton<MongoDbDatabaseScanner>();
        services.AddSingleton<IDatabaseScanner, DatabaseScannerRouter>();
        services.AddSingleton<IRepositoryScanner, RepositoryScanner>();

        services.AddSingleton<IConnector, GitHubRepositoryConnector>();
        services.AddSingleton<IConnector, GitHubActionsConnector>();
        services.AddSingleton<IConnector, GitLabRepositoryConnector>();
        services.AddSingleton<IConnector, AzureDevOpsRepositoryConnector>();
        services.AddSingleton<IConnector, AzurePipelinesConnector>();
        services.AddSingleton<IConnector, BitbucketRepositoryConnector>();
        services.AddSingleton<IConnector, SqlServerConnector>();
        services.AddSingleton<IConnector, PostgreSQLConnector>();
        services.AddSingleton<IConnector, MySqlConnectorImpl>();
        services.AddSingleton<IConnector, MongoDbConnector>();
        services.AddSingleton<IConnector, JenkinsConnector>();
        services.AddSingleton<IConnector, JiraConnector>();
        services.AddSingleton<IConnector, ServiceNowConnector>();
        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();

        services.AddScoped<ConnectorSyncJob>();
        services.AddScoped<RepositoryScanJob>();
        services.AddScoped<DatabaseScanJob>();
        services.AddScoped<GenerateRequirementsJob>();

        return services;
    }
}
