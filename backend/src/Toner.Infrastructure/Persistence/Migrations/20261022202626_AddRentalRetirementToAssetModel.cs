using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalRetirementToAssetModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RentalRetirementPrintThreshold",
                table: "AssetModels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RentalRetirementWarningPrints",
                table: "AssetModels",
                type: "integer",
                nullable: false,
                defaultValue: 50000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RentalRetirementPrintThreshold",
                table: "AssetModels");

            migrationBuilder.DropColumn(
                name: "RentalRetirementWarningPrints",
                table: "AssetModels");
        }
    }
}
