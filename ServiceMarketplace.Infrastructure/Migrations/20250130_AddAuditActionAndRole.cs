using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditActionAndRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "LoginAuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Login");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "LoginAuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "LoginAuditLogs");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "LoginAuditLogs");
        }
    }
}
