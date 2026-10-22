using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Calendar;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Technicians;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// El Inicio del técnico proyecta varias navegaciones anidadas (activo → sede → ciudad, tickets y órdenes) y un saldo de
// inventario agrupado: con InMemory solo se prueba la lógica, aquí corre contra el Postgres real.
[Collection(nameof(RlsFixtureCollection))]
public class TechnicianHomePostgresTests
{
    private readonly RlsFixture _fixture;

    public TechnicianHomePostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ContractA = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    [PostgresFact]
    public async Task Inicio_ArmaAgendaVisitaEnCursoYStockBajo_ContraPostgres()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-HOME-" + Guid.NewGuid().ToString("N")[..8];
        Guid techId = Guid.Empty, userId = Guid.Empty, zoneId = Guid.Empty, zoneLocationId = Guid.Empty, scheduleId = Guid.Empty;
        try
        {
            await using var db = OwnerContext();
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Tecnico) ?? TestEntities.Role(RoleNames.Tecnico);
            if (db.Entry(role).State == EntityState.Detached) db.Add(role);
            var user = TestEntities.User(role);
            var tech = TestEntities.Technician(user, isActive: true, status: TechnicianStatus.Disponible);
            var zone = new Zone { Name = marker };
            var zoneLocation = new InventoryLocation { Kind = InventoryLocationKind.Zona, Zone = zone };
            var item = new InventoryItem { Name = marker, Category = InventoryCategory.Repuesto, MinimumStock = 5 };
            db.AddRange(user, tech, zone, zoneLocation, item);
            await db.SaveChangesAsync();
            userId = user.Id; techId = tech.Id; zoneId = zone.Id; zoneLocationId = zoneLocation.Id;

            db.TechnicianZones.Add(new TechnicianZone { TechnicianId = tech.Id, ZoneId = zone.Id });
            db.InventoryMovements.Add(new InventoryMovement { InventoryItemId = item.Id, InventoryLocationId = zoneLocation.Id, Type = InventoryMovementType.Ajuste, Delta = 2 });

            var ticket = new ServiceTicket
            {
                ClientLocationId = LocationA, ClientId = RlsFixture.ClientA, AssetId = RlsFixture.AssetOfClientA, ReportedByUserId = userId,
                Description = marker, Status = ServiceTicketStatus.EnProceso, Priority = ServiceTicketPriority.Alta, TechnicianId = tech.Id
            };
            var schedule = TestEntities.MaintenanceSchedule(new Asset { Id = RlsFixture.AssetOfClientA }, new Contract { Id = ContractA });
            schedule.ClientId = RlsFixture.ClientA;
            scheduleId = schedule.Id;
            var order = new MaintenanceOrder
            {
                MaintenanceScheduleId = schedule.Id, AssetId = RlsFixture.AssetOfClientA, ClientId = RlsFixture.ClientA,
                Status = MaintenanceOrderStatus.Asignada, TechnicianId = tech.Id, ScheduledDate = DateTime.UtcNow, IncludesGeneral = true
            };
            db.AddRange(ticket, schedule, order);
            db.TimeLogs.Add(new TimeLog { TechnicianId = tech.Id, ServiceTicketId = ticket.Id, ClientId = RlsFixture.ClientA, StartTime = DateTime.UtcNow.AddMinutes(-15) });
            await db.SaveChangesAsync();

            var home = await new TechnicianHomeService(db, new WorkCalendarService(db, Microsoft.Extensions.Options.Options.Create(new WorkCalendarOptions())), TimeProvider.System).GetAsync(tech.Id);

            Assert.Equal(new[] { marker }, home.ZoneNames);
            Assert.Equal(new[] { "Ticket", "Orden" }, home.Agenda.Select(j => j.Kind));   // en curso primero
            Assert.Equal("Ticket", home.ActiveVisit!.Kind);
            Assert.NotNull(home.ActiveVisitStartedAt);
            Assert.All(home.Agenda, j => Assert.Equal("Sede A", j.LocationName));
            var low = Assert.Single(home.LowStock);
            Assert.Equal(marker, low.ItemName);
            Assert.Equal(2, low.Quantity);
        }
        finally
        {
            await using var owner = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
            await owner.OpenAsync();
            foreach (var sql in new[]
            {
                @"DELETE FROM ""TimeLogs"" WHERE ""TechnicianId"" = @t",
                @"DELETE FROM ""MaintenanceOrders"" WHERE ""TechnicianId"" = @t",
                @"DELETE FROM ""MaintenanceSchedules"" WHERE ""Id"" = @s",
                @"DELETE FROM ""ServiceTickets"" WHERE ""Description"" = @m",
                @"DELETE FROM ""InventoryMovements"" WHERE ""InventoryItemId"" IN (SELECT ""Id"" FROM ""InventoryItems"" WHERE ""Name"" = @m)",
                @"DELETE FROM ""InventoryItems"" WHERE ""Name"" = @m",
                @"DELETE FROM ""TechnicianZones"" WHERE ""TechnicianId"" = @t",
                @"DELETE FROM ""InventoryLocations"" WHERE ""Id"" = @l",
                @"DELETE FROM ""Zones"" WHERE ""Id"" = @z",
                @"DELETE FROM ""Technicians"" WHERE ""Id"" = @t",
                @"DELETE FROM ""Users"" WHERE ""Id"" = @u"
            })
            {
                await using var cmd = owner.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("t", techId);
                cmd.Parameters.AddWithValue("u", userId);
                cmd.Parameters.AddWithValue("z", zoneId);
                cmd.Parameters.AddWithValue("l", zoneLocationId);
                cmd.Parameters.AddWithValue("s", scheduleId);
                cmd.Parameters.AddWithValue("m", marker);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    // La subconsulta de "desde cuándo espera" (Max sobre el historial de estados) y el NOT EXISTS de visitas en curso solo
    // se traducen a SQL de verdad aquí; InMemory no dice nada de eso.
    [PostgresFact]
    public async Task Inicio_IncluyeInstalacionesPendientesDeLaZona_ContraPostgres()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-HOMEI-" + Guid.NewGuid().ToString("N")[..8];
        var userIds = new List<Guid>();
        var zoneId = Guid.Empty;
        var cityId = Guid.Empty;
        Guid clientId = Guid.Empty, brandId = Guid.Empty;
        try
        {
            await using var db = OwnerContext();
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Tecnico) ?? TestEntities.Role(RoleNames.Tecnico);
            if (db.Entry(role).State == EntityState.Detached) db.Add(role);
            var user = TestEntities.User(role);
            var rival = TestEntities.User(role);
            var tech = TestEntities.Technician(user);
            var rivalTech = TestEntities.Technician(rival);
            var city = TestEntities.City(marker);
            var coverage = TestEntities.Coverage(tech, city);
            var client = TestEntities.Client(marker);
            var location = TestEntities.ClientLocation(client, city);
            var brand = TestEntities.AssetBrand(marker);
            var model = TestEntities.AssetModel(brand, "MP-H1");
            var free = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);
            var taken = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);
            db.AddRange(user, rival, tech, rivalTech, city, coverage, client, location, brand, model);
            await db.SaveChangesAsync();
            db.AddRange(free, taken);
            await db.SaveChangesAsync();
            var waitingSince = DateTime.UtcNow.AddDays(-3);
            db.AssetStatusLogs.Add(new AssetStatusLog
            {
                AssetId = free.Id, ClientId = client.Id, PreviousStatus = AssetLifecycleStatus.EnBodega,
                NewStatus = AssetLifecycleStatus.PendienteInstalacion, ChangedAt = waitingSince
            });
            db.TimeLogs.Add(new TimeLog { TechnicianId = rivalTech.Id, AssetId = taken.Id, StartTime = DateTime.UtcNow.AddMinutes(-5) });
            await db.SaveChangesAsync();
            userIds.AddRange(new[] { user.Id, rival.Id });
            zoneId = coverage.ZoneId;
            cityId = city.Id;
            clientId = client.Id;
            brandId = brand.Id;

            var home = await new TechnicianHomeService(db, new WorkCalendarService(db, Microsoft.Extensions.Options.Options.Create(new WorkCalendarOptions())), TimeProvider.System).GetAsync(tech.Id);

            var job = Assert.Single(home.Agenda);
            Assert.Equal("Instalación", job.Kind);
            Assert.Equal(free.Id, job.Id);
            Assert.Equal(waitingSince, job.Since, TimeSpan.FromSeconds(1)); // del historial de estados, no de la creación
            Assert.Equal(marker + " MP-H1", job.Title);
        }
        finally
        {
            await using var cleanup = OwnerContext();
            async Task Sql(string sql, params object[] args) => await cleanup.Database.ExecuteSqlRawAsync(sql, args);
            await Sql("DELETE FROM \"TimeLogs\" WHERE \"TechnicianId\" IN (SELECT \"Id\" FROM \"Technicians\" WHERE \"UserId\" = ANY({0}))", userIds.ToArray());
            await Sql("DELETE FROM \"AssetStatusLogs\" WHERE \"AssetId\" IN (SELECT a.\"Id\" FROM \"Assets\" a JOIN \"AssetModels\" m ON m.\"Id\" = a.\"AssetModelId\" WHERE m.\"AssetBrandId\" = {0})", brandId);
            await Sql("DELETE FROM \"Assets\" WHERE \"AssetModelId\" IN (SELECT \"Id\" FROM \"AssetModels\" WHERE \"AssetBrandId\" = {0})", brandId);
            await Sql("DELETE FROM \"AssetModels\" WHERE \"AssetBrandId\" = {0}", brandId);
            await Sql("DELETE FROM \"AssetBrands\" WHERE \"Id\" = {0}", brandId);
            await Sql("DELETE FROM \"ClientLocations\" WHERE \"ClientId\" = {0}", clientId);
            await Sql("DELETE FROM \"Clients\" WHERE \"Id\" = {0}", clientId);
            await Sql("DELETE FROM \"TechnicianZones\" WHERE \"ZoneId\" = {0}", zoneId);
            await Sql("DELETE FROM \"Technicians\" WHERE \"UserId\" = ANY({0})", userIds.ToArray());
            await Sql("DELETE FROM \"Users\" WHERE \"Id\" = ANY({0})", userIds.ToArray());
            await Sql("DELETE FROM \"Cities\" WHERE \"Id\" = {0}", cityId);
            await Sql("DELETE FROM \"Zones\" WHERE \"Id\" = {0}", zoneId);
        }
    }
}
