using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForceTenantRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
            {
                "Tickets", "GraphNodes", "ConnectorInstances", "AuditLogs", "Recommendations",
                "Workspaces", "BuildRuns", "ReleaseSchedules"
            })
            {
                migrationBuilder.Sql($"""ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
            {
                "Tickets", "GraphNodes", "ConnectorInstances", "AuditLogs", "Recommendations",
                "Workspaces", "BuildRuns", "ReleaseSchedules"
            })
            {
                migrationBuilder.Sql($"""ALTER TABLE "{table}" NO FORCE ROW LEVEL SECURITY;""");
            }
        }
    }
}
