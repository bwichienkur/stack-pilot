using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;

namespace StackPilot.Infrastructure.Connectors;

public static class ConnectorDefinitionCatalog
{
    public static IReadOnlyList<ConnectorDefinition> All { get; } =
    [
        new()
        {
            Type = "github_repository",
            Name = "GitHub Repository",
            Category = ConnectorCategory.SourceCode,
            Description = "Connect to GitHub repositories for code scanning and indexing",
            ConfigSchema = """{"type":"object","properties":{"owner":{"type":"string"},"repositories":{"type":"string"}}}""",
            Capabilities = """["repository_scan","code_indexing","webhook"]"""
        },
        new()
        {
            Type = "gitlab_repository",
            Name = "GitLab Repository",
            Category = ConnectorCategory.SourceCode,
            Description = "Connect to GitLab projects for code scanning and CI/CD tracking",
            ConfigSchema = """{"type":"object","properties":{"baseUrl":{"type":"string"},"group":{"type":"string"},"projects":{"type":"string"}}}""",
            Capabilities = """["repository_scan","code_indexing","cicd_tracking"]"""
        },
        new()
        {
            Type = "azure_devops_repository",
            Name = "Azure DevOps Repos",
            Category = ConnectorCategory.SourceCode,
            Description = "Connect to Azure DevOps Git repositories for inventory and scanning",
            ConfigSchema = """{"type":"object","properties":{"organization":{"type":"string"},"project":{"type":"string"},"repositories":{"type":"string"}}}""",
            Capabilities = """["repository_scan","code_indexing"]"""
        },
        new()
        {
            Type = "bitbucket_repository",
            Name = "Bitbucket Repository",
            Category = ConnectorCategory.SourceCode,
            Description = "Connect to Bitbucket Cloud repositories for code inventory",
            ConfigSchema = """{"type":"object","properties":{"workspace":{"type":"string"},"repositories":{"type":"string"}}}""",
            Capabilities = """["repository_scan","code_indexing"]"""
        },
        new()
        {
            Type = "sql_server",
            Name = "SQL Server",
            Category = ConnectorCategory.Data,
            Description = "Connect to SQL Server databases for schema discovery",
            ConfigSchema = """{"type":"object","properties":{"server":{"type":"string"},"databases":{"type":"string"}}}""",
            Capabilities = """["database_scan"]"""
        },
        new()
        {
            Type = "postgresql",
            Name = "PostgreSQL",
            Category = ConnectorCategory.Data,
            Description = "Connect to PostgreSQL databases for schema discovery",
            ConfigSchema = """{"type":"object","properties":{"host":{"type":"string"},"port":{"type":"string"},"databases":{"type":"string"}}}""",
            Capabilities = """["database_scan"]"""
        },
        new()
        {
            Type = "mysql",
            Name = "MySQL",
            Category = ConnectorCategory.Data,
            Description = "Connect to MySQL databases for schema discovery",
            ConfigSchema = """{"type":"object","properties":{"host":{"type":"string"},"port":{"type":"string"},"databases":{"type":"string"}}}""",
            Capabilities = """["database_scan"]"""
        },
        new()
        {
            Type = "mongodb",
            Name = "MongoDB",
            Category = ConnectorCategory.Data,
            Description = "Connect to MongoDB clusters for collection inventory and schema hints",
            ConfigSchema = """{"type":"object","properties":{"databases":{"type":"string"}}}""",
            Capabilities = """["database_scan"]"""
        },
        new()
        {
            Type = "github_actions",
            Name = "GitHub Actions",
            Category = ConnectorCategory.CiCd,
            Description = "Track CI/CD builds and deployments via GitHub Actions",
            ConfigSchema = """{"type":"object","properties":{"owner":{"type":"string"},"repositories":{"type":"string"}}}""",
            Capabilities = """["cicd_tracking","deployment_tracking","webhook"]"""
        },
        new()
        {
            Type = "azure_pipelines",
            Name = "Azure Pipelines",
            Category = ConnectorCategory.CiCd,
            Description = "Track builds and releases from Azure Pipelines",
            ConfigSchema = """{"type":"object","properties":{"organization":{"type":"string"},"project":{"type":"string"},"pipelines":{"type":"string"}}}""",
            Capabilities = """["cicd_tracking","deployment_tracking"]"""
        },
        new()
        {
            Type = "jenkins",
            Name = "Jenkins",
            Category = ConnectorCategory.CiCd,
            Description = "Track Jenkins jobs and build history",
            ConfigSchema = """{"type":"object","properties":{"baseUrl":{"type":"string"},"jobs":{"type":"string"}}}""",
            Capabilities = """["cicd_tracking","deployment_tracking"]"""
        },
        new()
        {
            Type = "jira",
            Name = "Jira",
            Category = ConnectorCategory.Itsm,
            Description = "Sync Jira Cloud issues into StackPilot tickets",
            ConfigSchema = """{"type":"object","properties":{"baseUrl":{"type":"string"},"projects":{"type":"string"}}}""",
            Capabilities = """["ticket_sync"]"""
        },
        new()
        {
            Type = "servicenow",
            Name = "ServiceNow",
            Category = ConnectorCategory.Itsm,
            Description = "Sync ServiceNow incidents and tasks into StackPilot tickets",
            ConfigSchema = """{"type":"object","properties":{"instanceUrl":{"type":"string"},"table":{"type":"string"},"query":{"type":"string"}}}""",
            Capabilities = """["ticket_sync"]"""
        }
    ];

    public static ConnectorDefinition Create(string type)
    {
        var template = All.First(d => d.Type == type);
        return new ConnectorDefinition
        {
            Type = template.Type,
            Name = template.Name,
            Category = template.Category,
            Description = template.Description,
            ConfigSchema = template.ConfigSchema,
            Capabilities = template.Capabilities
        };
    }
}
