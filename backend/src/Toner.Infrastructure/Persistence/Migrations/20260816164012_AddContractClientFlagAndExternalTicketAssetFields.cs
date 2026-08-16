using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractClientFlagAndExternalTicketAssetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalAssetBrand",
                table: "ServiceTickets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExternalAssetCounter",
                table: "ServiceTickets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAssetModel",
                table: "ServiceTickets",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsContractClient",
                table: "Clients",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalAssetBrand",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "ExternalAssetCounter",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "ExternalAssetModel",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "IsContractClient",
                table: "Clients");
        }
    }
}
