using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email, ct))
            throw new InvalidOperationException("Email already registered");

        var user = new ApplicationUser
        {
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant(), ct)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedAccessException("This account uses SSO. Please sign in with your identity provider.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return CreateAuthResponse(user);
    }

    public async Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct);
        return user is null ? null : MapUser(user);
    }

    public async Task<AuthResponse> HandleSsoLoginAsync(string email, string? firstName, string? lastName, string externalId, string provider, CancellationToken ct = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.ExternalId == externalId && u.AuthProvider == provider, ct);

        if (user is null)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
            if (user is not null)
            {
                user.ExternalId = externalId;
                user.AuthProvider = provider;
                if (!string.IsNullOrEmpty(firstName)) user.FirstName = firstName;
                if (!string.IsNullOrEmpty(lastName)) user.LastName = lastName;
            }
        }

        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = normalizedEmail,
                FirstName = firstName,
                LastName = lastName,
                ExternalId = externalId,
                AuthProvider = provider,
                PasswordHash = string.Empty
            };
            _db.Users.Add(user);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return CreateAuthResponse(user);
    }

    private AuthResponse CreateAuthResponse(ApplicationUser user)
    {
        var token = GenerateJwt(user);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new AuthResponse(token, refresh, MapUser(user));
    }

    private string GenerateJwt(ApplicationUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config["Jwt:Key"] ?? "stackpilot-jwt-secret-key-min-32-chars-long!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("name", user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "StackPilot",
            audience: _config["Jwt:Audience"] ?? "StackPilot",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserDto MapUser(ApplicationUser u) =>
        new(u.Id, u.Email, u.FirstName, u.LastName, u.AvatarUrl);
}

public class OrganizationService : IOrganizationService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public OrganizationService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, Guid userId, CancellationToken ct = default)
    {
        var org = new Organization { Name = request.Name, Slug = request.Slug.ToLowerInvariant() };
        _db.Organizations.Add(org);

        var adminRole = await _db.Roles.FirstAsync(r => r.SystemRoleType == SystemRole.ClientAdmin, ct);
        _db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = userId,
            RoleId = adminRole.Id
        });

        var workspace = new Workspace
        {
            OrganizationId = org.Id,
            Name = "Default",
            Slug = "default",
            Description = "Default workspace"
        };
        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync(ct);

        return new OrganizationDto(org.Id, org.Name, org.Slug, org.Plan.ToString(), org.IsActive);
    }

    public async Task<List<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Select(m => new OrganizationDto(m.Organization.Id, m.Organization.Name, m.Organization.Slug, m.Organization.Plan.ToString(), m.Organization.IsActive))
            .ToListAsync(ct);
    }

    public async Task<OrganizationDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([id], ct);
        return org is null ? null : new OrganizationDto(org.Id, org.Name, org.Slug, org.Plan.ToString(), org.IsActive);
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(Guid orgId, CreateWorkspaceRequest request, CancellationToken ct = default)
    {
        var ws = new Workspace
        {
            OrganizationId = orgId,
            Name = request.Name,
            Slug = request.Slug.ToLowerInvariant(),
            Description = request.Description
        };
        _db.Workspaces.Add(ws);
        await _db.SaveChangesAsync(ct);
        return new WorkspaceDto(ws.Id, ws.OrganizationId, ws.Name, ws.Slug, ws.Description, ws.IsActive);
    }

    public async Task<List<WorkspaceDto>> GetWorkspacesAsync(Guid orgId, CancellationToken ct = default)
    {
        _tenant.SetOrganization(orgId);
        return await _db.Workspaces
            .Where(w => w.OrganizationId == orgId)
            .Select(w => new WorkspaceDto(w.Id, w.OrganizationId, w.Name, w.Slug, w.Description, w.IsActive))
            .ToListAsync(ct);
    }
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public AuditService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task LogAsync(string action, string? entityType = null, Guid? entityId = null, string? details = null, CancellationToken ct = default)
    {
        if (_tenant.OrganizationId is null) return;

        _db.AuditLogs.Add(new AuditLog
        {
            OrganizationId = _tenant.OrganizationId.Value,
            UserId = _tenant.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = details
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLogDto>> GetLogsAsync(Guid orgId, PagedRequest request, CancellationToken ct = default)
    {
        _tenant.SetOrganization(orgId);
        var query = _db.AuditLogs.AsQueryable();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new AuditLogDto(l.Id, l.Action, l.EntityType, l.EntityId, l.UserId, l.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }
}
