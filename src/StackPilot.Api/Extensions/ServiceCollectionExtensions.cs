using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace StackPilot.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStackPilotOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "StackPilot.Api";
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("StackPilot.*");

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                else
                    tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                else
                    metrics.AddConsoleExporter();
            });

        return services;
    }

    public static IServiceCollection AddStackPilotAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("STACKPILOT_JWT_KEY");
        if (string.IsNullOrWhiteSpace(jwtKey))
            jwtKey = "stackpilot-jwt-secret-key-min-32-chars-long!";

        var oidcEnabled = configuration.GetValue<bool>("Authentication:Oidc:Enabled");

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "StackPilot",
                ValidAudience = configuration["Jwt:Audience"] ?? "StackPilot",
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey))
            };
        });

        if (oidcEnabled)
        {
            authBuilder.AddOpenIdConnect("oidc", options =>
            {
                options.Authority = configuration["Authentication:Oidc:Authority"];
                options.ClientId = configuration["Authentication:Oidc:ClientId"];
                options.ClientSecret = configuration["Authentication:Oidc:ClientSecret"];
                options.ResponseType = "code";
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.CallbackPath = "/api/v1/auth/oidc/callback";
            });
        }

        return services;
    }
}
