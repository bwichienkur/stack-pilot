using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            const string policy = """
                (
                  NULLIF(current_setting('stackpilot.organization_id', true), '') IS NULL
                  OR "OrganizationId" = NULLIF(current_setting('stackpilot.organization_id', true), '')::uuid
                )
                """;

            foreach (var table in new[] { "Workspaces", "BuildRuns", "ReleaseSchedules" })
            {
                migrationBuilder.Sql($"""ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;""");
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS stackpilot_tenant_isolation ON "{table}";
                    CREATE POLICY stackpilot_tenant_isolation ON "{table}"
                    FOR ALL
                    USING {policy}
                    WITH CHECK {policy};
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Workspaces", "BuildRuns", "ReleaseSchedules" })
            {
                migrationBuilder.Sql($"""DROP POLICY IF EXISTS stackpilot_tenant_isolation ON "{table}";""");
                migrationBuilder.Sql($"""ALTER TABLE "{table}" DISABLE ROW LEVEL SECURITY;""");
            }
        }
    }
}
