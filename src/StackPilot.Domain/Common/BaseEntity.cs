namespace StackPilot.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public interface ITenantEntity
{
    Guid OrganizationId { get; set; }
}

public interface IWorkspaceScoped
{
    Guid? WorkspaceId { get; set; }
}
