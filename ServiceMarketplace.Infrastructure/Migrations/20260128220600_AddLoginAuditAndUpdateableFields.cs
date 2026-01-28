using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginAuditAndUpdateableFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ServiceRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Bids",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoginAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "JWT"),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoginTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LogoutTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditLog_LoginTime",
                table: "LoginAuditLogs",
                column: "LoginTime",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditLog_SessionId",
                table: "LoginAuditLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditLog_UserId",
                table: "LoginAuditLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginAuditLogs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Bids");
        }
    }
}
