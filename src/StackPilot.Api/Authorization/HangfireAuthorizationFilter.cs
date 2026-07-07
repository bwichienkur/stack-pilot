using Hangfire.Dashboard;

namespace StackPilot.Api.Authorization;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var env = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();

        if (env.IsDevelopment())
            return true;

        var apiKey = config["Hangfire:DashboardApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            return false;

        if (httpContext.Request.Headers.TryGetValue("X-Hangfire-Key", out var header) && header == apiKey)
            return true;

        if (httpContext.Request.Query.TryGetValue("apiKey", out var query) && query == apiKey)
            return true;

        return false;
    }
}
