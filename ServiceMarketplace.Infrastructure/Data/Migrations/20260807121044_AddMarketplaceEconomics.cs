using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceEconomics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProviderPayoutId",
                table: "UserNotifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceOrderDisputeId",
                table: "UserNotifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeAmount",
                table: "ServiceOrderPayments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlatformPaymentIntentId",
                table: "ServiceOrderPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderPayoutAmount",
                table: "ServiceOrderPayments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlatformPaymentIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlatformFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProviderPayoutAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    GatewayReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GatewayPaymentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VerificationNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VerifiedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformPaymentIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformPaymentIntents_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderPayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceOrderPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlatformFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayoutAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    PayoutReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MarkedPaidByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderPayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderPayouts_ServiceOrderPayments_ServiceOrderPaymentId",
                        column: x => x.ServiceOrderPaymentId,
                        principalTable: "ServiceOrderPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderPayouts_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrderDisputes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceOrderPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RaisedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AgainstUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOrderDisputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceOrderDisputes_ServiceOrderPayments_ServiceOrderPayme~",
                        column: x => x.ServiceOrderPaymentId,
                        principalTable: "ServiceOrderPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ServiceOrderDisputes_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_ProviderPayoutId",
                table: "UserNotifications",
                column: "ProviderPayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_ServiceOrderDisputeId",
                table: "UserNotifications",
                column: "ServiceOrderDisputeId");

            migrationBuilder.CreateIndex(
                name: "UX_ServiceOrderPayments_PlatformPaymentIntentId",
                table: "ServiceOrderPayments",
                column: "PlatformPaymentIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformPaymentIntents_CustomerStatusCreated",
                table: "PlatformPaymentIntents",
                columns: new[] { "CustomerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformPaymentIntents_OrderStatusCreated",
                table: "PlatformPaymentIntents",
                columns: new[] { "ServiceOrderId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_PlatformPaymentIntents_GatewayReference",
                table: "PlatformPaymentIntents",
                column: "GatewayReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPayouts_ProviderStatusCreated",
                table: "ProviderPayouts",
                columns: new[] { "ProviderId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPayouts_ServiceOrderId",
                table: "ProviderPayouts",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPayouts_StatusCreated",
                table: "ProviderPayouts",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ProviderPayouts_ServiceOrderPaymentId",
                table: "ProviderPayouts",
                column: "ServiceOrderPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDisputes_OrderStatusCreated",
                table: "ServiceOrderDisputes",
                columns: new[] { "ServiceOrderId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDisputes_ServiceOrderPaymentId",
                table: "ServiceOrderDisputes",
                column: "ServiceOrderPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDisputes_StatusCreated",
                table: "ServiceOrderDisputes",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrderPayments_PlatformPaymentIntents_PlatformPayment~",
                table: "ServiceOrderPayments",
                column: "PlatformPaymentIntentId",
                principalTable: "PlatformPaymentIntents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrderPayments_PlatformPaymentIntents_PlatformPayment~",
                table: "ServiceOrderPayments");

            migrationBuilder.DropTable(
                name: "PlatformPaymentIntents");

            migrationBuilder.DropTable(
                name: "ProviderPayouts");

            migrationBuilder.DropTable(
                name: "ServiceOrderDisputes");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_ProviderPayoutId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_ServiceOrderDisputeId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "UX_ServiceOrderPayments_PlatformPaymentIntentId",
                table: "ServiceOrderPayments");

            migrationBuilder.DropColumn(
                name: "ProviderPayoutId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "ServiceOrderDisputeId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "PlatformFeeAmount",
                table: "ServiceOrderPayments");

            migrationBuilder.DropColumn(
                name: "PlatformPaymentIntentId",
                table: "ServiceOrderPayments");

            migrationBuilder.DropColumn(
                name: "ProviderPayoutAmount",
                table: "ServiceOrderPayments");
        }
    }
}
