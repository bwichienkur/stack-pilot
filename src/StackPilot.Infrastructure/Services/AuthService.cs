using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StackPilot.Application.Billing;
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
    private readonly IHostEnvironment _env;

    public AuthService(AppDbContext db, IConfiguration configuration, IHostEnvironment env)
    {
        _db = db;
        _config = configuration;
        _env = env;
    }

    public Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        RegisterUserAsync(request, ct);

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
        return await CreateAuthResponseAsync(user, ct);
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
        return await CreateAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new UnauthorizedAccessException("User not found");
        return await CreateAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshWithTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token");

        if (!stored.IsActive)
            throw new UnauthorizedAccessException("Refresh token expired or revoked");

        stored.RevokedAt = DateTime.UtcNow;
        var response = await CreateAuthResponseAsync(stored.User, ct, stored.TokenHash);
        await _db.SaveChangesAsync(ct);
        return response;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(ApplicationUser user, CancellationToken ct, string? replacedTokenHash = null)
    {
        var token = await GenerateJwtAsync(user, ct);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshHash = HashToken(refresh);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            ReplacedByTokenHash = replacedTokenHash
        });

        if (replacedTokenHash is not null)
        {
            var old = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == replacedTokenHash, ct);
            if (old is not null) old.ReplacedByTokenHash = refreshHash;
        }

        await _db.SaveChangesAsync(ct);
        return new AuthResponse(token, refresh, MapUser(user));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private async Task<AuthResponse> RegisterUserAsync(RegisterRequest request, CancellationToken ct)
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
        return await CreateAuthResponseAsync(user, ct);
    }

    private async Task<string> GenerateJwtAsync(ApplicationUser user, CancellationToken ct)
    {
        var jwtKey = GetJwtKey();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("name", user.FullName)
        };

        var memberships = await _db.OrganizationMembers
            .AsNoTracking()
            .Include(m => m.Role)
                .ThenInclude(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(m => m.UserId == user.Id)
            .ToListAsync(ct);

        foreach (var membership in memberships)
        {
            var orgId = membership.OrganizationId.ToString();
            claims.Add(new Claim(StackPilotClaimTypes.Organization, orgId));

            if (membership.Role.SystemRoleType is SystemRole roleType)
                claims.Add(new Claim(StackPilotClaimTypes.OrgRole, $"{orgId}:{roleType}"));

            foreach (var rp in membership.Role.RolePermissions)
                claims.Add(new Claim(StackPilotClaimTypes.OrgPermission, $"{orgId}:{rp.Permission.Code}"));
        }

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "StackPilot",
            audience: _config["Jwt:Audience"] ?? "StackPilot",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GetJwtKey()
    {
        var key = _config["Jwt:Key"] ?? Environment.GetEnvironmentVariable("STACKPILOT_JWT_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            if (_env.IsProduction())
                throw new InvalidOperationException("JWT key must be configured via Jwt:Key or STACKPILOT_JWT_KEY in production");
            key = "stackpilot-jwt-secret-key-min-32-chars-long!";
        }
        return key;
    }

    private static UserDto MapUser(ApplicationUser u) =>
        new(u.Id, u.Email, u.FirstName, u.LastName, u.AvatarUrl);
}

public class OrganizationService : IOrganizationService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IApprovalGateService _approvalGates;
    private readonly IPlanLimitService _planLimits;
    private readonly IConfiguration _configuration;

    public OrganizationService(
        AppDbContext db,
        ITenantContext tenant,
        IApprovalGateService approvalGates,
        IPlanLimitService planLimits,
        IConfiguration configuration)
    {
        _db = db;
        _tenant = tenant;
        _approvalGates = approvalGates;
        _planLimits = planLimits;
        _configuration = configuration;
    }

    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, Guid userId, CancellationToken ct = default)
    {
        var org = new Organization
        {
            Name = request.Name,
            Slug = request.Slug.ToLowerInvariant(),
            Plan = OrganizationPlan.Trial,
            SubscriptionStatus = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(PlanCatalog.TrialDays),
            SettingsJson = JsonSerializer.Serialize(new { featureFlags = OrganizationFeatureFlags.Default })
        };
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
        await _approvalGates.EnsureDefaultGatesAsync(org.Id, ct);

        return new OrganizationDto(org.Id, org.Name, org.Slug, org.Plan.ToString(), org.IsActive);
    }

    public async Task<List<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Select(m => new OrganizationDto(m.Organization.Id, m.Organization.Name, m.Organization.Slug, m.Organization.Plan.ToString(), m.Organization.IsActive))
            .ToListAsync(ct);
    }

    public async Task<OrganizationDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var isMember = await _db.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == id && m.UserId == userId, ct);
        if (!isMember) return null;

        var org = await _db.Organizations.FindAsync([id], ct);
        return org is null ? null : new OrganizationDto(org.Id, org.Name, org.Slug, org.Plan.ToString(), org.IsActive);
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(Guid orgId, CreateWorkspaceRequest request, CancellationToken ct = default)
    {
        await _planLimits.EnsureCanCreateWorkspaceAsync(orgId, ct);

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

    public async Task<OrganizationSettingsDto> GetSettingsAsync(Guid orgId, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([orgId], ct)
            ?? throw new KeyNotFoundException("Organization not found");
        return MapSettings(org);
    }

    public async Task<OrganizationSettingsDto> UpdateSettingsAsync(Guid orgId, UpdateOrganizationSettingsRequest request, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([orgId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        if (!string.IsNullOrWhiteSpace(request.Name))
            org.Name = request.Name.Trim();

        var (flags, slack, _) = ReadAllSettings(org.SettingsJson);
        if (request.FeatureFlags is not null) flags = request.FeatureFlags;
        if (request.SlackWebhookUrl is not null) slack = request.SlackWebhookUrl;

        org.SettingsJson = SerializeSettings(flags, slack, ReadSamlSettings(org.SettingsJson));
        await _db.SaveChangesAsync(ct);
        return MapSettings(org);
    }

    public async Task<List<OrganizationMemberDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default)
    {
        return await _db.OrganizationMembers
            .Where(m => m.OrganizationId == orgId)
            .Include(m => m.User)
            .Include(m => m.Role)
            .OrderBy(m => m.User.Email)
            .Select(m => new OrganizationMemberDto(
                m.UserId, m.User.Email, m.User.FirstName, m.User.LastName, m.Role.Name, m.JoinedAt))
            .ToListAsync(ct);
    }

    public async Task<List<RoleDto>> GetInvitableRolesAsync(CancellationToken ct = default) =>
        await _db.Roles
            .Where(r => r.IsSystem && r.SystemRoleType != SystemRole.PlatformSuperAdmin)
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description))
            .ToListAsync(ct);

    public async Task<OrganizationInviteCreatedDto> CreateInviteAsync(Guid orgId, CreateInviteRequest request, Guid invitedByUserId, CancellationToken ct = default)
    {
        await _planLimits.EnsureCanAddSeatAsync(orgId, ct);

        var email = request.Email.ToLowerInvariant();
        if (await _db.OrganizationMembers.AnyAsync(m => m.OrganizationId == orgId && m.User.Email == email, ct))
            throw new InvalidOperationException("User is already a member of this organization");

        if (await _db.OrganizationInvites.AnyAsync(i =>
                i.OrganizationId == orgId && i.Email == email && i.AcceptedAt == null && i.ExpiresAt > DateTime.UtcNow, ct))
            throw new InvalidOperationException("A pending invite already exists for this email");

        var role = await ResolveInviteRoleAsync(request, ct);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var invite = new OrganizationInvite
        {
            OrganizationId = orgId,
            Email = email,
            RoleId = role.Id,
            TokenHash = HashInviteToken(token),
            InvitedByUserId = invitedByUserId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.OrganizationInvites.Add(invite);
        await _db.SaveChangesAsync(ct);

        var frontendUrl = (_configuration["Frontend:Url"] ?? "http://localhost:3000").TrimEnd('/');
        var inviteUrl = $"{frontendUrl}/invite/accept?token={Uri.EscapeDataString(token)}";

        return new OrganizationInviteCreatedDto(
            invite.Id, invite.Email, role.Name, invite.ExpiresAt, invite.AcceptedAt, invite.CreatedAt, token, inviteUrl);
    }

    private async Task<Role> ResolveInviteRoleAsync(CreateInviteRequest request, CancellationToken ct)
    {
        if (request.RoleId is Guid roleId && roleId != Guid.Empty)
        {
            return await _db.Roles.FindAsync([roleId], ct)
                ?? throw new KeyNotFoundException("Role not found");
        }

        if (string.IsNullOrWhiteSpace(request.RoleName))
            throw new ArgumentException("RoleId or RoleName is required");

        var normalized = request.RoleName.Replace(" ", "", StringComparison.Ordinal);
        return await _db.Roles.FirstOrDefaultAsync(r => r.Name == normalized || r.Name == request.RoleName, ct)
            ?? throw new KeyNotFoundException("Role not found");
    }

    public async Task<List<OrganizationInviteDto>> GetInvitesAsync(Guid orgId, CancellationToken ct = default)
    {
        return await _db.OrganizationInvites
            .Where(i => i.OrganizationId == orgId && i.AcceptedAt == null)
            .Include(i => i.Role)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new OrganizationInviteDto(i.Id, i.Email, i.Role.Name, i.ExpiresAt, i.AcceptedAt, i.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task RevokeInviteAsync(Guid orgId, Guid inviteId, CancellationToken ct = default)
    {
        var invite = await _db.OrganizationInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.OrganizationId == orgId && i.AcceptedAt == null, ct)
            ?? throw new KeyNotFoundException("Invite not found");

        _db.OrganizationInvites.Remove(invite);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<OrganizationDto> AcceptInviteAsync(AcceptInviteRequest request, Guid userId, CancellationToken ct = default)
    {
        var tokenHash = HashInviteToken(request.Token);
        var invite = await _db.OrganizationInvites
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct)
            ?? throw new KeyNotFoundException("Invalid or expired invite");

        if (invite.AcceptedAt is not null)
            throw new InvalidOperationException("Invite has already been accepted");
        if (invite.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Invite has expired");

        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found");

        if (!user.Email.Equals(invite.Email, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Invite email does not match your account");

        if (await _db.OrganizationMembers.AnyAsync(m => m.OrganizationId == invite.OrganizationId && m.UserId == userId, ct))
            throw new InvalidOperationException("You are already a member of this organization");

        await _planLimits.EnsureCanAddSeatAsync(invite.OrganizationId, ct);

        _db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = invite.OrganizationId,
            UserId = userId,
            RoleId = invite.RoleId
        });

        invite.AcceptedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var org = invite.Organization;
        return new OrganizationDto(org.Id, org.Name, org.Slug, org.Plan.ToString(), org.IsActive);
    }

    public async Task<OrganizationMemberDto> UpdateMemberRoleAsync(Guid orgId, UpdateMemberRoleRequest request, CancellationToken ct = default)
    {
        var member = await _db.OrganizationMembers
            .Include(m => m.User)
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == request.UserId, ct)
            ?? throw new KeyNotFoundException("Member not found");

        var role = await _db.Roles.FindAsync([request.RoleId], ct)
            ?? throw new KeyNotFoundException("Role not found");

        member.RoleId = request.RoleId;
        await _db.SaveChangesAsync(ct);

        return new OrganizationMemberDto(member.UserId, member.User.Email, member.User.FirstName, member.User.LastName, role.Name, member.JoinedAt);
    }

    public async Task<OrganizationSamlConfigDto> GetSamlConfigAsync(Guid orgId, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([orgId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        var saml = ReadSamlSettings(org.SettingsJson);
        var apiBase = (_configuration["Authentication:Saml:AcsUrl"] ?? "")
            .Replace("/api/v1/auth/saml/Acs", "", StringComparison.OrdinalIgnoreCase)
            .Replace("/api/v1/auth/saml/acs", "", StringComparison.OrdinalIgnoreCase)
            .Replace("/api/v1/auth/saml", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
        if (string.IsNullOrWhiteSpace(apiBase))
            apiBase = "http://localhost:5000";

        return new OrganizationSamlConfigDto(
            saml.Enabled,
            saml.EntityId ?? $"stackpilot-{org.Slug}",
            saml.IdpMetadataUrl,
            MaskCertificate(saml.IdpCertificate),
            $"{apiBase}/api/v1/auth/saml/signin?orgSlug={org.Slug}",
            $"{apiBase}/api/v1/auth/saml/metadata?orgSlug={org.Slug}");
    }

    public async Task<OrganizationSamlConfigDto> UpdateSamlConfigAsync(
        Guid orgId, UpdateOrganizationSamlConfigRequest request, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([orgId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        if (!PlanCatalog.LimitsFor(org.Plan).SamlSso)
            throw new InvalidOperationException("SAML SSO requires Professional plan or higher");

        var (flags, slack, saml) = ReadAllSettings(org.SettingsJson);
        saml = saml with
        {
            Enabled = request.Enabled,
            EntityId = request.EntityId?.Trim(),
            IdpMetadataUrl = request.IdpMetadataUrl?.Trim(),
            IdpCertificate = string.IsNullOrWhiteSpace(request.IdpCertificate)
                ? saml.IdpCertificate
                : request.IdpCertificate.Trim()
        };

        org.SettingsJson = SerializeSettings(flags, slack, saml);
        await _db.SaveChangesAsync(ct);
        return await GetSamlConfigAsync(orgId, ct);
    }

    private static string? MaskCertificate(string? pem)
    {
        if (string.IsNullOrWhiteSpace(pem)) return null;
        return pem.Length <= 40 ? "***" : $"{pem[..20]}...{pem[^20..]}";
    }

    private sealed record SamlSettings(bool Enabled, string? EntityId, string? IdpMetadataUrl, string? IdpCertificate);

    private static SamlSettings ReadSamlSettings(string? settingsJson)
    {
        var (_, _, saml) = ReadAllSettings(settingsJson);
        return saml;
    }

    private static (Dictionary<string, bool> Flags, string? Slack, SamlSettings Saml) ReadAllSettings(string? settingsJson)
    {
        var flags = OrganizationFeatureFlags.Default;
        string? slack = null;
        var saml = new SamlSettings(false, null, null, null);
        if (string.IsNullOrEmpty(settingsJson)) return (flags, slack, saml);

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("featureFlags", out var ff))
            {
                foreach (var prop in ff.EnumerateObject())
                    flags[prop.Name] = prop.Value.GetBoolean();
            }
            if (doc.RootElement.TryGetProperty("slackWebhookUrl", out var slackProp))
                slack = slackProp.GetString();
            if (doc.RootElement.TryGetProperty("saml", out var samlProp))
            {
                saml = new SamlSettings(
                    samlProp.TryGetProperty("enabled", out var en) && en.GetBoolean(),
                    samlProp.TryGetProperty("entityId", out var eid) ? eid.GetString() : null,
                    samlProp.TryGetProperty("idpMetadataUrl", out var meta) ? meta.GetString() : null,
                    samlProp.TryGetProperty("idpCertificate", out var cert) ? cert.GetString() : null);
            }
        }
        catch { /* defaults */ }

        return (flags, slack, saml);
    }

    private static string SerializeSettings(Dictionary<string, bool> flags, string? slack, SamlSettings saml) =>
        JsonSerializer.Serialize(new
        {
            featureFlags = flags,
            slackWebhookUrl = slack,
            saml = new
            {
                enabled = saml.Enabled,
                entityId = saml.EntityId,
                idpMetadataUrl = saml.IdpMetadataUrl,
                idpCertificate = saml.IdpCertificate
            }
        });

    private static string HashInviteToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static OrganizationSettingsDto MapSettings(Organization org)
    {
        var (flags, slack, _) = ReadAllSettings(org.SettingsJson);
        return new OrganizationSettingsDto(org.Id, org.Name, org.Slug, org.Plan.ToString(), flags, slack);
    }

    private static (Dictionary<string, bool> Flags, string? Slack) ReadSettings(string? settingsJson)
    {
        var (flags, slack, _) = ReadAllSettings(settingsJson);
        return (flags, slack);
    }
}

public static class OrganizationFeatureFlags
{
    public static readonly string[] All =
    [
        "applications", "docs", "recommendations", "qa", "uat", "deployments"
    ];

    public static Dictionary<string, bool> Default => All.ToDictionary(k => k, _ => true);
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
        // Fail-closed: do not allow callers to read audit logs for an organization different
        // from the organization established by the tenant middleware (X-Organization-Id).
        if (_tenant.OrganizationId is null)
            throw new UnauthorizedAccessException("Organization context required");

        if (_tenant.OrganizationId.Value != orgId)
            throw new UnauthorizedAccessException("Cross-tenant audit log access is not allowed");

        _tenant.SetOrganization(orgId);
        var query = _db.AuditLogs.AsQueryable();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new AuditLogDto(l.Id, l.Action, l.EntityType, l.EntityId, l.UserId, l.DetailsJson, l.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }
}
