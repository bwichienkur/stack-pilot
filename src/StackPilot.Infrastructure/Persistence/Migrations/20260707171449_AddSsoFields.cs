using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSsoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthProvider",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AuthProvider_ExternalId",
                table: "Users",
                columns: new[] { "AuthProvider", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_AuthProvider_ExternalId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AuthProvider",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Users");
        }
    }
}
