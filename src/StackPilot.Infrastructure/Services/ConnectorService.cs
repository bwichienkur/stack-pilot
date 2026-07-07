using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Application.Connectors;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class ConnectorService : IConnectorService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IConnectorRegistry _registry;
    private readonly ICredentialEncryptionService _encryption;
    private readonly IBackgroundJobService _jobs;
    private readonly IAuditService _audit;

    public ConnectorService(AppDbContext db, ITenantContext tenant, IConnectorRegistry registry,
        ICredentialEncryptionService encryption, IBackgroundJobService jobs, IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _registry = registry;
        _encryption = encryption;
        _jobs = jobs;
        _audit = audit;
    }

    public async Task<List<ConnectorDefinitionDto>> GetDefinitionsAsync(CancellationToken ct = default)
    {
        var defs = await _db.ConnectorDefinitions.ToListAsync(ct);
        return defs.Select(d => new ConnectorDefinitionDto(d.Id, d.Type, d.Name, d.Description, d.ConfigSchema,
            JsonSerializer.Deserialize<string[]>(d.Capabilities) ?? Array.Empty<string>())).ToList();
    }

    public async Task<List<ConnectorInstanceDto>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default)
    {
        return await _db.ConnectorInstances
            .Include(c => c.Definition)
            .Where(c => c.WorkspaceId == workspaceId)
            .Select(c => new ConnectorInstanceDto(c.Id, c.Name, c.Definition.Type, c.Status.ToString(),
                c.HealthStatus.ToString(), c.LastSyncAt, c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ConnectorInstanceDto> CreateAsync(Guid workspaceId, CreateConnectorRequest request, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.FindAsync([workspaceId], ct)
            ?? throw new KeyNotFoundException("Workspace not found");

        _tenant.SetOrganization(ws.OrganizationId);

        var instance = new ConnectorInstance
        {
            OrganizationId = ws.OrganizationId,
            WorkspaceId = workspaceId,
            DefinitionId = request.DefinitionId,
            Name = request.Name,
            ConfigJson = request.ConfigJson,
            Status = ConnectorStatus.Pending
        };
        _db.ConnectorInstances.Add(instance);

        if (request.Credentials is not null)
        {
            foreach (var (type, value) in request.Credentials)
            {
                _db.ConnectorCredentials.Add(new ConnectorCredential
                {
                    ConnectorId = instance.Id,
                    CredentialType = type,
                    EncryptedValue = _encryption.Encrypt(value, ws.OrganizationId)
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("connector.created", "ConnectorInstance", instance.Id, ct: ct);

        var def = await _db.ConnectorDefinitions.FindAsync([request.DefinitionId], ct);
        return new ConnectorInstanceDto(instance.Id, instance.Name, def!.Type, instance.Status.ToString(),
            instance.HealthStatus.ToString(), null, instance.CreatedAt);
    }

    public async Task<bool> TestConnectionAsync(Guid connectorId, CancellationToken ct = default)
    {
        var (connector, context) = await BuildContextAsync(connectorId, ct);
        var result = await connector.TestConnectionAsync(context, ct);

        var instance = await _db.ConnectorInstances.FindAsync([connectorId], ct);
        if (instance is not null)
        {
            instance.HealthStatus = result.Success ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            instance.LastHealthAt = DateTime.UtcNow;
            instance.Status = result.Success ? ConnectorStatus.Active : ConnectorStatus.Unhealthy;
            await _db.SaveChangesAsync(ct);
        }

        return result.Success;
    }

    public async Task<SyncHistoryDto> TriggerSyncAsync(Guid connectorId, CancellationToken ct = default)
    {
        var history = new SyncHistory
        {
            ConnectorId = connectorId,
            Status = SyncStatus.Pending,
            StartedAt = DateTime.UtcNow
        };
        _db.SyncHistories.Add(history);
        await _db.SaveChangesAsync(ct);

        _jobs.EnqueueConnectorSync(connectorId);
        await _audit.LogAsync("connector.sync_triggered", "ConnectorInstance", connectorId, ct: ct);

        return new SyncHistoryDto(history.Id, history.Status.ToString(), history.StartedAt, null, 0);
    }

    public async Task<List<SyncHistoryDto>> GetSyncHistoryAsync(Guid connectorId, CancellationToken ct = default)
    {
        return await _db.SyncHistories
            .Where(s => s.ConnectorId == connectorId)
            .OrderByDescending(s => s.StartedAt)
            .Take(50)
            .Select(s => new SyncHistoryDto(s.Id, s.Status.ToString(), s.StartedAt, s.CompletedAt, s.ItemsProcessed))
            .ToListAsync(ct);
    }

    private async Task<(IConnector connector, ConnectorContext context)> BuildContextAsync(Guid connectorId, CancellationToken ct)
    {
        var instance = await _db.ConnectorInstances
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .FirstOrDefaultAsync(c => c.Id == connectorId, ct)
            ?? throw new KeyNotFoundException("Connector not found");

        var credentials = instance.Credentials.ToDictionary(
            c => c.CredentialType,
            c => _encryption.Decrypt(c.EncryptedValue, instance.OrganizationId));

        var context = new ConnectorContext
        {
            OrganizationId = instance.OrganizationId,
            WorkspaceId = instance.WorkspaceId,
            ConnectorInstanceId = instance.Id,
            ConnectorType = instance.Definition.Type,
            ConfigJson = instance.ConfigJson,
            Credentials = credentials
        };

        return (_registry.Resolve(instance.Definition.Type), context);
    }
}
