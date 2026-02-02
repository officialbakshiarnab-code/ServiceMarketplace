using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceMarketplace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingColumnsToRequestsAndBids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Category and Location columns to ServiceRequests
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ServiceRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "ServiceRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // Add Message column to Bids (nullable, as it's optional)
            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Bids",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove columns in reverse order
            migrationBuilder.DropColumn(
                name: "Message",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ServiceRequests");
        }
    }
}
