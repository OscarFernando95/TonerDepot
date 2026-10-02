using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// La cobertura de un técnico se deriva de sus zonas (Zone → City.ZoneId). Con InMemory solo se prueba la lógica;
// aquí las consultas que usan el motor de asignación y CoveredCityIds corren contra el Postgres real, para
// confirmar que traducen y que respetan la zona.
[Collection(nameof(RlsFixtureCollection))]
public class ZoneCoveragePostgresTests
{
    private readonly RlsFixture _fixture;

    public ZoneCoveragePostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private sealed record Seeded(Guid ZoneId, Guid TechnicianId, Guid UserId);

    private static async Task<Seeded> SeedAsync(bool technicianInZone)
    {
        await using var db = OwnerContext();
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Tecnico);
        if (role is null)
        {
            role = TestEntities.Role(RoleNames.Tecnico);
            db.Add(role);
        }

        var user = TestEntities.User(role);
        var technician = TestEntities.Technician(user, isActive: true, status: TechnicianStatus.Disponible);
        var zone = new Zone { Name = "Zona PG " + Guid.NewGuid().ToString("N")[..8] };
        db.AddRange(user, technician, zone);
        await db.SaveChangesAsync();

        var city = await db.Cities.SingleAsync(c => c.Id == RlsFixture.CityId);
        city.ZoneId = zone.Id;
        if (technicianInZone)
        {
            db.TechnicianZones.Add(new TechnicianZone { TechnicianId = technician.Id, ZoneId = zone.Id });
        }

        await db.SaveChangesAsync();
        return new Seeded(zone.Id, technician.Id, user.Id);
    }

    private static async Task CleanupAsync(Seeded s)
    {
        await using var owner = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
        await owner.OpenAsync();
        foreach (var sql in new[]
        {
            @"UPDATE ""Cities"" SET ""ZoneId"" = NULL WHERE ""ZoneId"" = @z",
            @"DELETE FROM ""TechnicianZones"" WHERE ""ZoneId"" = @z",
            @"DELETE FROM ""Zones"" WHERE ""Id"" = @z",
            @"DELETE FROM ""Technicians"" WHERE ""Id"" = @t",
            @"DELETE FROM ""Users"" WHERE ""Id"" = @u"
        })
        {
            await using var cmd = owner.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("z", s.ZoneId);
            cmd.Parameters.AddWithValue("t", s.TechnicianId);
            cmd.Parameters.AddWithValue("u", s.UserId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static ServiceTicket UnsavedTicket(Guid reporterId) => new()
    {
        ClientLocationId = LocationA,
        ClientId = RlsFixture.ClientA,
        ReportedByUserId = reporterId,
        Description = "ZonePG",
        Status = ServiceTicketStatus.Abierto,
        Priority = ServiceTicketPriority.Media
    };

    [PostgresFact]
    public async Task ElMotor_AsignaAlTecnicoDeLaZonaDelMunicipio()
    {
        await _fixture.EnsureSeededAsync();
        var s = await SeedAsync(technicianInZone: true);
        try
        {
            await using var db = OwnerContext();
            var ticket = UnsavedTicket(s.UserId);
            db.ServiceTickets.Add(ticket);

            await TestAssignment.Create(db).AssignServiceTicketAsync(ticket);

            Assert.Equal(s.TechnicianId, ticket.TechnicianId);
            Assert.Equal(ServiceTicketStatus.Asignado, ticket.Status);
        }
        finally
        {
            await CleanupAsync(s);
        }
    }

    [PostgresFact]
    public async Task ElMotor_NoAsignaSiElTecnicoNoEsDeEsaZona()
    {
        await _fixture.EnsureSeededAsync();
        var s = await SeedAsync(technicianInZone: false);
        try
        {
            await using var db = OwnerContext();
            var ticket = UnsavedTicket(s.UserId);
            db.ServiceTickets.Add(ticket);

            await TestAssignment.Create(db).AssignServiceTicketAsync(ticket);

            Assert.NotEqual(s.TechnicianId, ticket.TechnicianId);
        }
        finally
        {
            await CleanupAsync(s);
        }
    }

    [PostgresFact]
    public async Task CoveredCityIds_DevuelveLosMunicipiosDeLasZonasDelTecnico()
    {
        await _fixture.EnsureSeededAsync();
        var s = await SeedAsync(technicianInZone: true);
        try
        {
            await using var db = OwnerContext();

            Assert.Equal(new[] { RlsFixture.CityId }, await db.CoveredCityIds(s.TechnicianId).ToListAsync());
            Assert.Equal(s.ZoneId, await db.ZoneIdOfCityAsync(RlsFixture.CityId));
        }
        finally
        {
            await CleanupAsync(s);
        }
    }
}
