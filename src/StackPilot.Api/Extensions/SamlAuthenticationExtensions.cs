using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
using StackPilot.Api.Saml;
using StackPilot.Application.Interfaces;

namespace StackPilot.Api.Extensions;

public static class SamlAuthenticationExtensions
{
    public const string SchemeName = Saml2Defaults.Scheme;

    public static AuthenticationBuilder AddStackPilotSaml(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration,
        IServiceCollection services)
    {
        if (!configuration.GetValue<bool>("Authentication:Saml:Enabled"))
            return authBuilder;

        services.AddHttpContextAccessor();
        services.AddScoped<SamlOrgIdentityProviderLoader>();
        services.AddSingleton<IPostConfigureOptions<Saml2Options>, Saml2OptionsPostConfigure>();

        return authBuilder.AddSaml2(options =>
        {
            var entityId = configuration["Authentication:Saml:EntityId"] ?? "stackpilot";
            options.SPOptions.EntityId = new EntityId(entityId);
            options.SPOptions.ModulePath = "/api/v1/auth/saml";
            options.SPOptions.ReturnUrl = new Uri(configuration["Frontend:Url"] ?? "http://localhost:3000");

            var idpEntityId = configuration["Authentication:Saml:IdpEntityId"];
            var idpCert = configuration["Authentication:Saml:IdpCertificate"];
            var idpSsoUrl = configuration["Authentication:Saml:IdpSsoUrl"];

            if (!string.IsNullOrWhiteSpace(idpEntityId) && !string.IsNullOrWhiteSpace(idpCert))
            {
                var idp = SamlIdentityProviderFactory.Create(idpEntityId, idpSsoUrl, idpCert, options.SPOptions);
                options.IdentityProviders.Add(idp);
            }
        });
    }
}

internal sealed class Saml2OptionsPostConfigure : IPostConfigureOptions<Saml2Options>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _scopeFactory;

    public Saml2OptionsPostConfigure(IHttpContextAccessor httpContextAccessor, IServiceScopeFactory scopeFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _scopeFactory = scopeFactory;
    }

    public void PostConfigure(string? name, Saml2Options options)
    {
        options.Notifications.GetIdentityProvider = (idpEntity, relayData, sp) =>
        {
            if (relayData is null || !relayData.TryGetValue("orgSlug", out var orgSlug) || string.IsNullOrWhiteSpace(orgSlug))
                return null;

            using var scope = _scopeFactory.CreateScope();
            var loader = scope.ServiceProvider.GetRequiredService<SamlOrgIdentityProviderLoader>();
            return loader.TryLoadFromOrgSlugAsync(orgSlug, sp.SPOptions).GetAwaiter().GetResult();
        };

        options.Notifications.AcsCommandResultCreated = async (commandResult, _) =>
        {
            if (commandResult.Principal?.Identity?.IsAuthenticated != true) return;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null) return;

            var email = commandResult.Principal.FindFirst(ClaimTypes.Email)?.Value
                ?? commandResult.Principal.FindFirst("email")?.Value
                ?? commandResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sub = commandResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? email;
            var given = commandResult.Principal.FindFirst(ClaimTypes.GivenName)?.Value;
            var family = commandResult.Principal.FindFirst(ClaimTypes.Surname)?.Value;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(sub)) return;

            var auth = httpContext.RequestServices.GetRequiredService<IAuthService>();
            var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
            var authResponse = await auth.HandleSsoLoginAsync(email, given, family, sub, "saml", httpContext.RequestAborted);

            var frontendUrl = config["Frontend:Url"] ?? "http://localhost:3000";
            commandResult.Location = new Uri($"{frontendUrl}/login#access_token={Uri.EscapeDataString(authResponse.AccessToken)}");
            commandResult.HandledResult = true;
        };
    }
}
