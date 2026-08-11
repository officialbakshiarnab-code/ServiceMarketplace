using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCoverageAndBidComparison : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServiceCategoryId",
                table: "ServiceProviderProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceZoneId",
                table: "ServiceProviderProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedDurationMinutes",
                table: "Bids",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProviderProfiles_ServiceCategoryId",
                table: "ServiceProviderProfiles",
                column: "ServiceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProviderProfiles_ServiceZoneId",
                table: "ServiceProviderProfiles",
                column: "ServiceZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceProviderProfiles_ServiceCategories_ServiceCategoryId",
                table: "ServiceProviderProfiles",
                column: "ServiceCategoryId",
                principalTable: "ServiceCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceProviderProfiles_ServiceZones_ServiceZoneId",
                table: "ServiceProviderProfiles",
                column: "ServiceZoneId",
                principalTable: "ServiceZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceProviderProfiles_ServiceCategories_ServiceCategoryId",
                table: "ServiceProviderProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceProviderProfiles_ServiceZones_ServiceZoneId",
                table: "ServiceProviderProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ServiceProviderProfiles_ServiceCategoryId",
                table: "ServiceProviderProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ServiceProviderProfiles_ServiceZoneId",
                table: "ServiceProviderProfiles");

            migrationBuilder.DropColumn(
                name: "ServiceCategoryId",
                table: "ServiceProviderProfiles");

            migrationBuilder.DropColumn(
                name: "ServiceZoneId",
                table: "ServiceProviderProfiles");

            migrationBuilder.DropColumn(
                name: "EstimatedDurationMinutes",
                table: "Bids");
        }
    }
}
