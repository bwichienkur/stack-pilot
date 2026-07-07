using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPgVectorAndTicketExternalRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingVector",
                table: "GraphChunks",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_Tickets_WorkspaceId",
                table: "Tickets");

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_WorkspaceId_ExternalReference",
                table: "Tickets",
                columns: new[] { "WorkspaceId", "ExternalReference" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_WorkspaceId_ExternalReference",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EmbeddingVector",
                table: "GraphChunks");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_WorkspaceId",
                table: "Tickets",
                column: "WorkspaceId");
        }
    }
}
