using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Domain.Common;
using StackPilot.Domain.Entities;

namespace StackPilot.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EnvironmentConfig> EnvironmentConfigs => Set<EnvironmentConfig>();
    public DbSet<ConnectorDefinition> ConnectorDefinitions => Set<ConnectorDefinition>();
    public DbSet<ConnectorInstance> ConnectorInstances => Set<ConnectorInstance>();
    public DbSet<ConnectorCredential> ConnectorCredentials => Set<ConnectorCredential>();
    public DbSet<SyncHistory> SyncHistories => Set<SyncHistory>();
    public DbSet<GraphNode> GraphNodes => Set<GraphNode>();
    public DbSet<GraphEdge> GraphEdges => Set<GraphEdge>();
    public DbSet<RepositoryScan> RepositoryScans => Set<RepositoryScan>();
    public DbSet<DatabaseScan> DatabaseScans => Set<DatabaseScan>();
    public DbSet<DocumentationPage> DocumentationPages => Set<DocumentationPage>();
    public DbSet<DocumentationVersion> DocumentationVersions => Set<DocumentationVersion>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<QaEvidence> QaEvidences => Set<QaEvidence>();
    public DbSet<UatDecision> UatDecisions => Set<UatDecision>();
    public DbSet<ReleaseSchedule> ReleaseSchedules => Set<ReleaseSchedule>();
    public DbSet<BuildRun> BuildRuns => Set<BuildRun>();
    public DbSet<AiAction> AiActions => Set<AiAction>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Organization>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<Workspace>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            e.HasOne(x => x.Organization).WithMany(x => x.Workspaces).HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => new { x.AuthProvider, x.ExternalId });
        });

        modelBuilder.Entity<OrganizationMember>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.PermissionId });
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<ConnectorDefinition>(e =>
        {
            e.HasIndex(x => x.Type).IsUnique();
        });

        modelBuilder.Entity<GraphEdge>(e =>
        {
            e.HasIndex(x => new { x.SourceNodeId, x.TargetNodeId, x.EdgeType }).IsUnique();
            e.HasOne(x => x.SourceNode).WithMany(x => x.OutgoingEdges).HasForeignKey(x => x.SourceNodeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.TargetNode).WithMany(x => x.IncomingEdges).HasForeignKey(x => x.TargetNodeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GraphNode>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.NodeType });
        });

        modelBuilder.Entity<Ticket>(e =>
        {
            e.Property(x => x.TicketNumber).UseIdentityColumn();
            e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DocumentationVersion>(e =>
        {
            e.HasIndex(x => new { x.PageId, x.Version }).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        });

        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        void TenantFilter<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
            where T : class, ITenantEntity
        {
            entity.HasQueryFilter(e =>
                !_tenantContext.IsTenantFilterEnabled ||
                (_tenantContext.OrganizationId != null && e.OrganizationId == _tenantContext.OrganizationId));
        }

        TenantFilter(modelBuilder.Entity<Workspace>());
        TenantFilter(modelBuilder.Entity<ConnectorInstance>());
        TenantFilter(modelBuilder.Entity<GraphNode>());
        TenantFilter(modelBuilder.Entity<GraphEdge>());
        TenantFilter(modelBuilder.Entity<Ticket>());
        TenantFilter(modelBuilder.Entity<Recommendation>());
        TenantFilter(modelBuilder.Entity<DocumentationPage>());
        TenantFilter(modelBuilder.Entity<RepositoryScan>());
        TenantFilter(modelBuilder.Entity<DatabaseScan>());
        TenantFilter(modelBuilder.Entity<AiAction>());
        TenantFilter(modelBuilder.Entity<AiConversation>());
        TenantFilter(modelBuilder.Entity<Approval>());
        TenantFilter(modelBuilder.Entity<BuildRun>());
        TenantFilter(modelBuilder.Entity<Team>());
        TenantFilter(modelBuilder.Entity<EnvironmentConfig>());

        modelBuilder.Entity<AuditLog>().HasQueryFilter(e =>
            !_tenantContext.IsTenantFilterEnabled ||
            (_tenantContext.OrganizationId != null && e.OrganizationId == _tenantContext.OrganizationId));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
