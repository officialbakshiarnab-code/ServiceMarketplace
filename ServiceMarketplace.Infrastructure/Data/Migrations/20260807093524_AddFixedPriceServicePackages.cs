using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedPriceServicePackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AcceptedBidId",
                table: "ServiceOrders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ServicePackageId",
                table: "ServiceOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServicePackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ServiceCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServicePackages_ServiceCategories_ServiceCategoryId",
                        column: x => x.ServiceCategoryId,
                        principalTable: "ServiceCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServicePackages_ServiceZones_ServiceZoneId",
                        column: x => x.ServiceZoneId,
                        principalTable: "ServiceZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_ServicePackageId",
                table: "ServiceOrders",
                column: "ServicePackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePackages_ActiveCategoryZone",
                table: "ServicePackages",
                columns: new[] { "IsActive", "ServiceCategoryId", "ServiceZoneId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePackages_ProviderId",
                table: "ServicePackages",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePackages_ServiceCategoryId",
                table: "ServicePackages",
                column: "ServiceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePackages_ServiceZoneId",
                table: "ServicePackages",
                column: "ServiceZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrders_ServicePackages_ServicePackageId",
                table: "ServiceOrders",
                column: "ServicePackageId",
                principalTable: "ServicePackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrders_ServicePackages_ServicePackageId",
                table: "ServiceOrders");

            migrationBuilder.DropTable(
                name: "ServicePackages");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrders_ServicePackageId",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "ServicePackageId",
                table: "ServiceOrders");

            migrationBuilder.AlterColumn<Guid>(
                name: "AcceptedBidId",
                table: "ServiceOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
