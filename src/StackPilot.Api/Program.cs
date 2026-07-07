using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using Serilog;
using StackPilot.Api;
using StackPilot.Api.Authorization;
using StackPilot.Api.Extensions;
using StackPilot.Api.Middleware;
using StackPilot.Infrastructure;
using StackPilot.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddStackPilotAuthentication(builder.Configuration);
builder.Services.AddStackPilotAuthorization();
builder.Services.AddStackPilotOpenTelemetry(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("ai", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "StackPilot API",
        Version = "v1",
        Description = "Enterprise Software Intelligence Platform"
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=stackpilot;Username=stackpilot;Password=stackpilot";

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

    builder.Services.AddHangfireServer(options =>
    {
        options.Queues = ["critical", "default", "low"];
        options.WorkerCount = 4;
    });
}

var app = builder.Build();

await DatabaseSeeder.SeedAsync(app.Services);
await DatabaseSeeder.EnsureConnectorDefinitionsAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandling();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseTenantContext();
app.UseAuthorization();
app.MapControllers();
if (!app.Environment.IsEnvironment("Testing"))
{
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireAuthorizationFilter()]
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "StackPilot.Api", timestamp = DateTime.UtcNow }));

app.Run();

public partial class Program { }
