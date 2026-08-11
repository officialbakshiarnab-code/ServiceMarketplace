using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ServiceMarketplace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCatalogAndStructuredRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PreferredStartAt",
                table: "ServiceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requirements",
                table: "ServiceRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceCategoryId",
                table: "ServiceRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceZoneId",
                table: "ServiceRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Urgency",
                table: "ServiceRequests",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.CreateTable(
                name: "ServiceCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ZoneName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PinCodeRegion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceZones", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ServiceCategories",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "Slug", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Leaks, taps, pipes, fittings, and water-flow issues", true, "Plumbing", "plumbing", 10, null },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Wiring, fixtures, switchboards, fans, and basic electrical repairs", true, "Electrical", "electrical", 20, null },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"), new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Home and small-office cleaning services", true, "Cleaning", "cleaning", 30, null }
                });

            migrationBuilder.InsertData(
                table: "ServiceZones",
                columns: new[] { "Id", "City", "Country", "CreatedAt", "DisplayName", "IsActive", "PinCodeRegion", "SortOrder", "State", "UpdatedAt", "ZoneName" },
                values: new object[,]
                {
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"), "Kolkata", "India", new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Central Kolkata, Kolkata, West Bengal", true, "7000xx", 10, "West Bengal", null, "Central Kolkata" },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"), "Kolkata", "India", new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Utc), "South Kolkata, Kolkata, West Bengal", true, "7000xx", 20, "West Bengal", null, "South Kolkata" },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3"), "Kolkata", "India", new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Utc), "North Kolkata, Kolkata, West Bengal", true, "7000xx", 30, "West Bengal", null, "North Kolkata" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ServiceCategoryId",
                table: "ServiceRequests",
                column: "ServiceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ServiceZoneId",
                table: "ServiceRequests",
                column: "ServiceZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategories_ActiveSort",
                table: "ServiceCategories",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_ServiceCategories_Slug",
                table: "ServiceCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceZones_ActiveSort",
                table: "ServiceZones",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceZones_Area",
                table: "ServiceZones",
                columns: new[] { "State", "City", "ZoneName" });

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_ServiceCategories_ServiceCategoryId",
                table: "ServiceRequests",
                column: "ServiceCategoryId",
                principalTable: "ServiceCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_ServiceZones_ServiceZoneId",
                table: "ServiceRequests",
                column: "ServiceZoneId",
                principalTable: "ServiceZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_ServiceCategories_ServiceCategoryId",
                table: "ServiceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_ServiceZones_ServiceZoneId",
                table: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "ServiceCategories");

            migrationBuilder.DropTable(
                name: "ServiceZones");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_ServiceCategoryId",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_ServiceZoneId",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "PreferredStartAt",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "Requirements",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ServiceCategoryId",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ServiceZoneId",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "Urgency",
                table: "ServiceRequests");
        }
    }
}
