using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductDeliveryOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductDeliveryOrderId",
                table: "UserNotifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductDeliveryOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    DeliveryRecipientName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DeliveryPhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeliveryAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DeliveryCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeliveryState = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServiceZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuyerNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CancelledByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadyForPickupAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OutForDeliveryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDeliveryOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductDeliveryOrders_ProductListings_ProductListingId",
                        column: x => x.ProductListingId,
                        principalTable: "ProductListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductDeliveryOrders_SellerProfiles_SellerId",
                        column: x => x.SellerId,
                        principalTable: "SellerProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductDeliveryOrders_ServiceZones_ServiceZoneId",
                        column: x => x.ServiceZoneId,
                        principalTable: "ServiceZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_ProductDeliveryOrderId",
                table: "UserNotifications",
                column: "ProductDeliveryOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDeliveryOrders_BuyerStatusCreated",
                table: "ProductDeliveryOrders",
                columns: new[] { "BuyerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductDeliveryOrders_ProductListingId",
                table: "ProductDeliveryOrders",
                column: "ProductListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDeliveryOrders_SellerStatusCreated",
                table: "ProductDeliveryOrders",
                columns: new[] { "SellerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductDeliveryOrders_ServiceZoneId",
                table: "ProductDeliveryOrders",
                column: "ServiceZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductDeliveryOrders");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_ProductDeliveryOrderId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "ProductDeliveryOrderId",
                table: "UserNotifications");
        }
    }
}
