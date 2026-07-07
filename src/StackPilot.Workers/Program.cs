using Serilog;
using StackPilot.Infrastructure;
using StackPilot.Infrastructure.Extensions;
using StackPilot.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddStackPilotOpenTelemetry(builder.Configuration, "StackPilot.Workers", includeAspNetCoreInstrumentation: false);
builder.Services.AddStackPilotHangfire(builder.Configuration, builder.Environment, runServer: true);

var host = builder.Build();
await DatabaseSeeder.SeedAsync(host.Services);

Log.Information("StackPilot Workers starting — processing Hangfire queues: critical, default, low");
host.Run();
