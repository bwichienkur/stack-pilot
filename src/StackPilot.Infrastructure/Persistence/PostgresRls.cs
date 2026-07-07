using Microsoft.EntityFrameworkCore;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Persistence;

public static class PostgresRls
{
    public const string SettingName = "stackpilot.organization_id";

    public static async Task SetOrganizationAsync(this AppDbContext db, Guid? organizationId, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || db.Database.ProviderName?.Contains("Npgsql") != true)
            return;

        var value = organizationId?.ToString() ?? "";
        await db.Database.ExecuteSqlRawAsync($"SELECT set_config('{SettingName}', {{0}}, false)", value);
    }

    public static async Task ClearOrganizationAsync(this AppDbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || db.Database.ProviderName?.Contains("Npgsql") != true)
            return;

        await db.Database.ExecuteSqlRawAsync($"SELECT set_config('{SettingName}', '', false)");
    }
}
