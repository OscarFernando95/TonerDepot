using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupportCoverage",
                table: "Clients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "HorarioOficina");

            migrationBuilder.CreateTable(
                name: "CompanyHolidayOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyHolidayOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianTimeOffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianTimeOffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianTimeOffs_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianWorkIntervals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianWorkIntervals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianWorkIntervals_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyHolidayOverrides_Date",
                table: "CompanyHolidayOverrides",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTimeOffs_TechnicianId_StartsAt",
                table: "TechnicianTimeOffs",
                columns: new[] { "TechnicianId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianWorkIntervals_TechnicianId_Day",
                table: "TechnicianWorkIntervals",
                columns: new[] { "TechnicianId", "Day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyHolidayOverrides");

            migrationBuilder.DropTable(
                name: "TechnicianTimeOffs");

            migrationBuilder.DropTable(
                name: "TechnicianWorkIntervals");

            migrationBuilder.DropColumn(
                name: "SupportCoverage",
                table: "Clients");
        }
    }
}
