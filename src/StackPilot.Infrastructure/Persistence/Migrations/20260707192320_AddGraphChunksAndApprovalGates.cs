using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGraphChunksAndApprovalGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.CreateTable(
                name: "ApprovalGates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    GateType = table.Column<int>(type: "integer", nullable: false),
                    RequiredPermission = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalGates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GraphChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    GraphNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    EmbeddingJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GraphChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GraphChunks_GraphNodes_GraphNodeId",
                        column: x => x.GraphNodeId,
                        principalTable: "GraphNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalGates_OrganizationId_GateType",
                table: "ApprovalGates",
                columns: new[] { "OrganizationId", "GateType" });

            migrationBuilder.CreateIndex(
                name: "IX_GraphChunks_GraphNodeId",
                table: "GraphChunks",
                column: "GraphNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_GraphChunks_OrganizationId_WorkspaceId",
                table: "GraphChunks",
                columns: new[] { "OrganizationId", "WorkspaceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalGates");

            migrationBuilder.DropTable(
                name: "GraphChunks");
        }
    }
}
