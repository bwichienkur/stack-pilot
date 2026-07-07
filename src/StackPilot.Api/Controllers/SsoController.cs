using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;

namespace StackPilot.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class SsoController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IConfiguration _config;

    public SsoController(IAuthService auth, IConfiguration config)
    {
        _auth = auth;
        _config = config;
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
                loginUrl = "/api/v1/auth/oidc/login"
            });
        }
        return Ok(ApiResponse<object>.Ok(providers));
    }
}
