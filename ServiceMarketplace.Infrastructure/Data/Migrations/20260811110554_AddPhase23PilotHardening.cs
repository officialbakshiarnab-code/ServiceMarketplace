using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase23PilotHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenAt",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenByUserId",
                table: "Messages",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenReason",
                table: "Messages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ConversationRestricted",
                table: "MessageReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LegalHoldUntil",
                table: "MessageReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MessageHidden",
                table: "MessageReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LegalHoldUntil",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionNotes",
                table: "Conversations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_StatusRetainUntil",
                table: "Conversations",
                columns: new[] { "Status", "RetainUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_StatusRetainUntil",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "HiddenAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "HiddenByUserId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "HiddenReason",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ConversationRestricted",
                table: "MessageReports");

            migrationBuilder.DropColumn(
                name: "LegalHoldUntil",
                table: "MessageReports");

            migrationBuilder.DropColumn(
                name: "MessageHidden",
                table: "MessageReports");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "LegalHoldUntil",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "RetentionNotes",
                table: "Conversations");
        }
    }
}
