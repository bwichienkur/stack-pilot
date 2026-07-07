namespace StackPilot.Application.Common;

public interface ITenantContext
{
    Guid? OrganizationId { get; }
    Guid? WorkspaceId { get; }
    Guid? UserId { get; }
    void SetOrganization(Guid organizationId);
    void SetWorkspace(Guid workspaceId);
    void SetUser(Guid userId);
}

public class TenantContext : ITenantContext
{
    public Guid? OrganizationId { get; private set; }
    public Guid? WorkspaceId { get; private set; }
    public Guid? UserId { get; private set; }

    public void SetOrganization(Guid organizationId) => OrganizationId = organizationId;
    public void SetWorkspace(Guid workspaceId) => WorkspaceId = workspaceId;
    public void SetUser(Guid userId) => UserId = userId;
}

public class ApiResponse<T>
{
    public T? Data { get; set; }
    public ApiMeta? Meta { get; set; }
    public List<ApiError>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, ApiMeta? meta = null) => new() { Data = data, Meta = meta };
    public static ApiResponse<T> Fail(params ApiError[] errors) => new() { Errors = errors.ToList() };
}

public class ApiMeta
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Field { get; set; }
}

public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
