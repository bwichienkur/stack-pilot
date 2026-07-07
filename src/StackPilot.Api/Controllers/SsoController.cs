using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Billing;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Jobs;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class SsoController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public SsoController(IAuthService auth, IConfiguration config, AppDbContext db)
    {
        _auth = auth;
        _config = config;
        _db = db;
    }

    [HttpGet("oidc/login")]
    [AllowAnonymous]
    public IActionResult OidcLogin([FromQuery] string? returnUrl = null)
    {
        if (!_config.GetValue<bool>("Authentication:Oidc:Enabled"))
            return BadRequest(ApiResponse<object>.Fail(new ApiError { Code = "SSO_DISABLED", Message = "OIDC SSO is not enabled" }));

        var props = new AuthenticationProperties { RedirectUri = returnUrl ?? "/api/v1/auth/oidc/callback" };
        return Challenge(props, "oidc");
    }

    [HttpGet("oidc/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OidcCallback(CancellationToken ct)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync("oidc");
        if (!authenticateResult.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(new ApiError { Code = "SSO_FAILED", Message = "OIDC authentication failed" }));

        var claims = authenticateResult.Principal?.Claims.ToList() ?? [];
        var email = claims.FirstOrDefault(c => c.Type == "email")?.Value
            ?? claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
        var sub = claims.FirstOrDefault(c => c.Type == "sub")?.Value
            ?? claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var givenName = claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
        var familyName = claims.FirstOrDefault(c => c.Type == "family_name")?.Value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(sub))
            return BadRequest(ApiResponse<object>.Fail(new ApiError { Code = "SSO_INVALID", Message = "Missing required claims from identity provider" }));

        var authResponse = await _auth.HandleSsoLoginAsync(email, givenName, familyName, sub, "oidc", ct);
        var frontendUrl = _config["Frontend:Url"] ?? "http://localhost:3000";
        return Redirect($"{frontendUrl}/login#access_token={Uri.EscapeDataString(authResponse.AccessToken)}");
    }

    [HttpGet("sso/providers")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> GetProviders()
    {
        var providers = new List<object>();
        if (_config.GetValue<bool>("Authentication:Oidc:Enabled"))
        {
            providers.Add(new
            {
                type = "oidc",
                name = _config["Authentication:Oidc:DisplayName"] ?? "Enterprise SSO",
                loginUrl = "/api/v1/auth/oidc/login",
                requiresProfessionalPlan = false
            });
        }
        if (_config.GetValue<bool>("Authentication:Saml:Enabled"))
        {
            var devMode = _config.GetValue<bool>("Authentication:Saml:DevMode");
            var hasIdpCert = !string.IsNullOrWhiteSpace(_config["Authentication:Saml:IdpCertificate"]);
            providers.Add(new
            {
                type = "saml",
                name = _config["Authentication:Saml:DisplayName"] ?? "SAML 2.0 SSO",
                loginUrl = "/api/v1/auth/saml/login",
                metadataUrl = "/api/v1/auth/saml/metadata",
                requiresProfessionalPlan = true,
                productionReady = hasIdpCert && !devMode,
                devModeAvailable = devMode
            });
        }
        return Ok(ApiResponse<object>.Ok(providers));
    }

    [HttpGet("saml/login")]
    [AllowAnonymous]
    public async Task<IActionResult> SamlLogin([FromQuery] string? returnUrl = null, [FromQuery] string? orgSlug = null, CancellationToken ct = default)
    {
        if (!_config.GetValue<bool>("Authentication:Saml:Enabled"))
            return BadRequest(ApiResponse<object>.Fail(new ApiError { Code = "SSO_DISABLED", Message = "SAML SSO is not enabled" }));

        if (!string.IsNullOrWhiteSpace(orgSlug))
        {
            var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Slug == orgSlug.ToLowerInvariant(), ct);
            if (org is null)
                return NotFound(ApiResponse<object>.Fail(new ApiError { Code = "ORG_NOT_FOUND", Message = "Organization not found" }));

            if (!PlanCatalog.LimitsFor(org.Plan).SamlSso)
                return BadRequest(ApiResponse<object>.Fail(new ApiError
                {
                    Code = "PLAN_SAML_NOT_AVAILABLE",
                    Message = "SAML SSO requires Professional plan or higher"
                }));
        }

        var entityId = _config["Authentication:Saml:EntityId"] ?? "stackpilot";
        var acsUrl = _config["Authentication:Saml:AcsUrl"] ?? $"{Request.Scheme}://{Request.Host}/api/v1/auth/saml/acs";
        return Ok(ApiResponse<object>.Ok(new
        {
            message = "SAML SSO is configured. Initiate login via your IdP using SP metadata.",
            entityId,
            acsUrl,
            returnUrl,
            orgSlug
        }));
    }

    [HttpGet("saml/metadata")]
    [AllowAnonymous]
    public IActionResult SamlMetadata()
    {
        if (!_config.GetValue<bool>("Authentication:Saml:Enabled"))
            return NotFound();

        var entityId = _config["Authentication:Saml:EntityId"] ?? "stackpilot";
        var acsUrl = _config["Authentication:Saml:AcsUrl"] ?? $"{Request.Scheme}://{Request.Host}/api/v1/auth/saml/acs";
        var metadata = $"""<?xml version="1.0"?><EntityDescriptor xmlns="urn:oasis:names:tc:SAML:2.0:metadata" entityID="{entityId}"><SPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol"><AssertionConsumerService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST" Location="{acsUrl}" index="0"/></SPSSODescriptor></EntityDescriptor>""";
        return Content(metadata, "application/xml");
    }

    [HttpPost("saml/acs")]
    [AllowAnonymous]
    public async Task<IActionResult> SamlAcs([FromForm] string? email, [FromForm] string? orgSlug, CancellationToken ct = default)
    {
        if (!_config.GetValue<bool>("Authentication:Saml:Enabled"))
            return BadRequest(ApiResponse<object>.Fail(new ApiError { Code = "SSO_DISABLED", Message = "SAML SSO is not enabled" }));

        if (_config.GetValue<bool>("Authentication:Saml:DevMode") && !string.IsNullOrWhiteSpace(email))
        {
            if (!string.IsNullOrWhiteSpace(orgSlug))
            {
                var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Slug == orgSlug.ToLowerInvariant(), ct);
                if (org is not null && !PlanCatalog.LimitsFor(org.Plan).SamlSso)
                    return BadRequest(ApiResponse<object>.Fail(new ApiError
                    {
                        Code = "PLAN_SAML_NOT_AVAILABLE",
                        Message = "SAML SSO requires Professional plan or higher"
                    }));
            }

            var authResponse = await _auth.HandleSsoLoginAsync(email, "Dev", "User", $"saml-dev:{email}", "saml", ct);
            var frontendUrl = _config["Frontend:Url"] ?? "http://localhost:3000";
            return Redirect($"{frontendUrl}/login#access_token={Uri.EscapeDataString(authResponse.AccessToken)}");
        }

        return BadRequest(ApiResponse<object>.Fail(new ApiError
        {
            Code = "SAML_NOT_CONFIGURED",
            Message = "SAML assertion consumer is scaffolded. Configure Authentication:Saml:IdpCertificate and integrate Sustainsys.Saml2 for production, or enable Authentication:Saml:DevMode for demo login."
        }));
    }
}
