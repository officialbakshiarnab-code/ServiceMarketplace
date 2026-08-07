using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ServiceMarketplace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsedProductInspectionGuidance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "Condition",
                table: "ProductListings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<string>(
                name: "ConditionNotes",
                table: "ProductListings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasOriginalBill",
                table: "ProductListings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasWarranty",
                table: "ProductListings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InspectionChecklist",
                table: "ProductListings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseYear",
                table: "ProductListings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductInspectionPrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductInspectionPrompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductInspectionPrompts_ProductCategories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ProductInspectionPrompts",
                columns: new[] { "Id", "CreatedAt", "IsActive", "ProductCategoryId", "Prompt", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("dddddddd-dddd-dddd-dddd-ddddddddddd1"), new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc1"), "Check visible wear, stains, cracks, missing parts, and whether the product has been cleaned before pickup.", 10, null },
                    { new Guid("dddddddd-dddd-dddd-dddd-ddddddddddd2"), new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc2"), "Check rust, grip condition, moving parts, safety guards, serial/model labels, and included accessories.", 10, null },
                    { new Guid("dddddddd-dddd-dddd-dddd-ddddddddddd3"), new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc3"), "Power on the device, check battery/charging, ports, display, buttons, invoice/warranty status, and reset/lock status.", 10, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductListings_StatusConditionCategoryZone",
                table: "ProductListings",
                columns: new[] { "Status", "Condition", "ProductCategoryId", "ServiceZoneId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductInspectionPrompts_CategoryActiveSort",
                table: "ProductInspectionPrompts",
                columns: new[] { "ProductCategoryId", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductInspectionPrompts");

            migrationBuilder.DropIndex(
                name: "IX_ProductListings_StatusConditionCategoryZone",
                table: "ProductListings");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "ProductListings");

            migrationBuilder.DropColumn(
                name: "ConditionNotes",
                table: "ProductListings");

            migrationBuilder.DropColumn(
                name: "HasOriginalBill",
                table: "ProductListings");

            migrationBuilder.DropColumn(
                name: "HasWarranty",
                table: "ProductListings");

            migrationBuilder.DropColumn(
                name: "InspectionChecklist",
                table: "ProductListings");

            migrationBuilder.DropColumn(
                name: "PurchaseYear",
                table: "ProductListings");
        }
    }
}
