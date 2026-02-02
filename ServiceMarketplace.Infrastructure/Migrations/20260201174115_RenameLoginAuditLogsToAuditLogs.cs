using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameLoginAuditLogsToAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "LoginAuditLogs",
                newName: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "AuditLogs",
                newName: "EventType");

            migrationBuilder.RenameColumn(
                name: "LoginTime",
                table: "AuditLogs",
                newName: "TimestampUtc");

            migrationBuilder.RenameIndex(
                name: "IX_LoginAuditLog_LoginTime",
                table: "AuditLogs",
                newName: "IX_AuditLogs_TimestampUtc");

            migrationBuilder.RenameIndex(
                name: "IX_LoginAuditLog_SessionId",
                table: "AuditLogs",
                newName: "IX_AuditLogs_SessionId");

            migrationBuilder.RenameIndex(
                name: "IX_LoginAuditLog_UserId",
                table: "AuditLogs",
                newName: "IX_AuditLogs_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "SessionId",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.Sql(@"
UPDATE AuditLogs
SET TimestampUtc = CASE
    WHEN EventType = 'Logout' AND LogoutTime IS NOT NULL THEN LogoutTime
    ELSE TimestampUtc
END
");

            migrationBuilder.DropColumn(
                name: "LoginProvider",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "LogoutTime",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "AuditLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoginProvider",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "JWT");

            migrationBuilder.AddColumn<DateTime>(
                name: "LogoutTime",
                table: "AuditLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Platform",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SessionId",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "EventType",
                table: "AuditLogs",
                newName: "Action");

            migrationBuilder.RenameColumn(
                name: "TimestampUtc",
                table: "AuditLogs",
                newName: "LoginTime");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_TimestampUtc",
                table: "AuditLogs",
                newName: "IX_LoginAuditLog_LoginTime");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_SessionId",
                table: "AuditLogs",
                newName: "IX_LoginAuditLog_SessionId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                newName: "IX_LoginAuditLog_UserId");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "LoginAuditLogs");
        }
    }
}
