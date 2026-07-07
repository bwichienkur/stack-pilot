using StackPilot.Infrastructure;
using StackPilot.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
await DatabaseSeeder.SeedAsync(host.Services);
host.Run();
