using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceOrderAuditAndReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceOrderAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOrderAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceOrderAuditEvents_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderAuditEvents_ActorUserId",
                table: "ServiceOrderAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderAuditEvents_OrderCreated",
                table: "ServiceOrderAuditEvents",
                columns: new[] { "ServiceOrderId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceOrderAuditEvents");
        }
    }
}
