using StackPilot.Domain.Common;
using StackPilot.Domain.Enums;

namespace StackPilot.Domain.Entities;

public class ConnectorDefinition : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ConnectorCategory Category { get; set; } = ConnectorCategory.SourceCode;
    public string ConfigSchema { get; set; } = "{}";
    public string Capabilities { get; set; } = "[]";

    public ICollection<ConnectorInstance> Instances { get; set; } = [];
}

public class ConnectorInstance : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid DefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public ConnectorStatus Status { get; set; } = ConnectorStatus.Pending;
    public DateTime? LastSyncAt { get; set; }
    public DateTime? LastHealthAt { get; set; }
    public HealthStatus HealthStatus { get; set; } = HealthStatus.Unknown;

    public ConnectorDefinition Definition { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
    public ICollection<ConnectorCredential> Credentials { get; set; } = [];
    public ICollection<SyncHistory> SyncHistories { get; set; } = [];
}

public class ConnectorCredential : BaseEntity
{
    public Guid ConnectorId { get; set; }
    public string CredentialType { get; set; } = string.Empty;
    public byte[] EncryptedValue { get; set; } = [];
    public int KeyVersion { get; set; } = 1;
    public DateTime? ExpiresAt { get; set; }

    public ConnectorInstance Connector { get; set; } = null!;
}

public class SyncHistory : BaseEntity
{
    public Guid ConnectorId { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int ItemsProcessed { get; set; }
    public string? ErrorsJson { get; set; }

    public ConnectorInstance Connector { get; set; } = null!;
}
