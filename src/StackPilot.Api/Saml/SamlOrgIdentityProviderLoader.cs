using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Api.Saml;

public sealed class SamlOrgIdentityProviderLoader
{
    private readonly AppDbContext _db;

    public SamlOrgIdentityProviderLoader(AppDbContext db) => _db = db;

    public async Task<IdentityProvider?> TryLoadFromOrgSlugAsync(string? orgSlug, SPOptions spOptions, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orgSlug)) return null;

        var org = await _db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Slug == orgSlug.ToLowerInvariant(), ct);
        if (org is null) return null;

        var saml = ReadOrgSaml(org.SettingsJson);
        if (!saml.Enabled || string.IsNullOrWhiteSpace(saml.EntityId) || string.IsNullOrWhiteSpace(saml.IdpCertificate))
            return null;

        return SamlIdentityProviderFactory.Create(saml.EntityId, saml.IdpMetadataUrl, saml.IdpCertificate, spOptions);
    }

    private sealed record OrgSamlSettings(bool Enabled, string? EntityId, string? IdpMetadataUrl, string? IdpCertificate);

    private static OrgSamlSettings ReadOrgSaml(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson)) return new OrgSamlSettings(false, null, null, null);
        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (!doc.RootElement.TryGetProperty("saml", out var saml)) return new OrgSamlSettings(false, null, null, null);
            return new OrgSamlSettings(
                saml.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean(),
                saml.TryGetProperty("entityId", out var entityId) ? entityId.GetString() : null,
                saml.TryGetProperty("idpMetadataUrl", out var metadataUrl) ? metadataUrl.GetString() : null,
                saml.TryGetProperty("idpCertificate", out var cert) ? cert.GetString() : null);
        }
        catch
        {
            return new OrgSamlSettings(false, null, null, null);
        }
    }
}
