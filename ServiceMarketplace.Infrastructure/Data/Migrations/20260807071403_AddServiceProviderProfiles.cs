using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceProviderProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceProviderProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Bio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Skills = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PrimaryCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServiceAreaCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServiceAreaState = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServiceAreaZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IdentityVerificationSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    AddressVerificationSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    BackgroundCheckConsent = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceProviderProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceProviderProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProviderProfiles_ServiceArea",
                table: "ServiceProviderProfiles",
                columns: new[] { "ServiceAreaState", "ServiceAreaCity" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProviderProfiles_Status",
                table: "ServiceProviderProfiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_ServiceProviderProfiles_UserId",
                table: "ServiceProviderProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceProviderProfiles");
        }
    }
}
