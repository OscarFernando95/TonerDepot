using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Assets;
using Toner.Application.Common;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Tickets;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// Tickets de las máquinas vinculadas (subconsulta sobre TechnicianAssets) y datos de tóner de la lista de máquinas:
// contra el Postgres real.
[Collection(nameof(RlsFixtureCollection))]
public class TechnicianMachinesPostgresTests
{
    private readonly RlsFixture _fixture;

    public TechnicianMachinesPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    [PostgresFact]
    public async Task TicketsDeMaquinasVinculadas_YToner_ContraPostgres()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-MACH-" + Guid.NewGuid().ToString("N")[..8];
        Guid techId = Guid.Empty, userId = Guid.Empty;
        try
        {
            await using var db = OwnerContext();
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Tecnico) ?? TestEntities.Role(RoleNames.Tecnico);
            if (db.Entry(role).State == EntityState.Detached) db.Add(role);
            var user = TestEntities.User(role);
            var tech = TestEntities.Technician(user, isActive: true, status: TechnicianStatus.Disponible);
            var toner = new InventoryItem { Name = marker, Category = InventoryCategory.Toner };
            db.AddRange(user, tech, toner);
            await db.SaveChangesAsync();
            userId = user.Id; techId = tech.Id;

            db.TechnicianAssets.Add(new TechnicianAsset { TechnicianId = tech.Id, AssetId = RlsFixture.AssetOfClientA });
            ServiceTicket Ticket(Guid asset, ServiceTicketStatus status, Guid? who) => new()
            {
                ClientLocationId = LocationA, ClientId = RlsFixture.ClientA, AssetId = asset, ReportedByUserId = user.Id,
                Description = marker, Status = status, Priority = ServiceTicketPriority.Media, TechnicianId = who
            };
            db.ServiceTickets.AddRange(
                Ticket(RlsFixture.AssetOfClientA, ServiceTicketStatus.SinAsignar, null),
                Ticket(RlsFixture.AssetOfClientA, ServiceTicketStatus.Resuelto, null),
                Ticket(RlsFixture.AssetOfClientA, ServiceTicketStatus.Asignado, tech.Id),
                Ticket(RlsFixture.AssetOfClientB, ServiceTicketStatus.SinAsignar, null));
            var mainId = (await db.InventoryLocations.FirstAsync(l => l.Kind == InventoryLocationKind.Principal)).Id;
            db.InventoryMovements.Add(new InventoryMovement
            {
                InventoryItemId = toner.Id, InventoryLocationId = mainId, Type = InventoryMovementType.Consumo, Delta = -2,
                AssetId = RlsFixture.AssetOfClientA, ClientId = RlsFixture.ClientA, CounterValue = 777, OccurredAt = DateTime.UtcNow.AddDays(-3)
            });
            await db.SaveChangesAsync();

            var tickets = await new ServiceTicketService(db, TestAssignment.Create(db)).ListForLinkedMachinesAsync(tech.Id, null, null);
            var mine = tickets.Items.Where(t => t.AssetId == RlsFixture.AssetOfClientA).ToList();
            Assert.Single(mine);                       // solo el SinAsignar de mi máquina (no el resuelto ni el que ya es mío)
            Assert.DoesNotContain(tickets.Items, t => t.AssetId == RlsFixture.AssetOfClientB);

            var assets = new AssetService(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));
            var row = (await assets.ListForMeterReadingAsync(new RequestingUser(user.Id, RoleNames.Tecnico, null, tech.Id), null, null)).Items
                .Single(a => a.AssetId == RlsFixture.AssetOfClientA);
            Assert.Equal(777, row.LastTonerCounter);
            Assert.Equal(2, row.TonerUnitsLast90Days);
        }
        finally
        {
            await using var owner = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
            await owner.OpenAsync();
            foreach (var sql in new[]
            {
                @"DELETE FROM ""InventoryMovements"" WHERE ""InventoryItemId"" IN (SELECT ""Id"" FROM ""InventoryItems"" WHERE ""Name"" = @m)",
                @"DELETE FROM ""InventoryItems"" WHERE ""Name"" = @m",
                @"DELETE FROM ""ServiceTickets"" WHERE ""Description"" = @m",
                @"DELETE FROM ""TechnicianAssets"" WHERE ""TechnicianId"" = @t",
                @"DELETE FROM ""Technicians"" WHERE ""Id"" = @t",
                @"DELETE FROM ""Users"" WHERE ""Id"" = @u"
            })
            {
                await using var cmd = owner.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("m", marker);
                cmd.Parameters.AddWithValue("t", techId);
                cmd.Parameters.AddWithValue("u", userId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
