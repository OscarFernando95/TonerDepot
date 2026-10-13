using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-05: "una visita abierta por técnico" y "un técnico por instalación" eran check-then-act. Estos
// tests reproducen la carrera de forma DETERMINISTA contra el Postgres real y con el rol de la aplicación:
// sin índice UNIQUE parcial quedan dos TimeLog abiertos; con él, el segundo recibe un 409 legible.
[Collection(nameof(RlsFixtureCollection))]
public class TimeLogConcurrencyPostgresTests
{
    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly RlsFixture _fixture;

    public TimeLogConcurrencyPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(TenantContextAccessor tenant, params IInterceptor[] extra) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new IInterceptor[] { new TenantContextInterceptor(tenant) }.Concat(extra))
            .Options);

    // Pausa la petición en la que está instalado justo DESPUÉS de leer el técnico (ya vio "Disponible") y
    // deja que otra petición complete su check-in entero antes de seguir: la intercalación exacta de una
    // carrera real, sin depender del scheduler.
    private sealed class PauseAfterTechnicianRead : DbCommandInterceptor
    {
        private readonly Func<Task> _runOther;
        private bool _fired;

        public PauseAfterTechnicianRead(Func<Task> runOther) => _runOther = runOther;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!_fired && command.CommandText.Contains("FROM \"Technicians\""))
            {
                _fired = true;
                await _runOther();
            }

            return result;
        }
    }

    private sealed record Seed(Guid UserId, Guid TechnicianId, Guid TicketA, Guid TicketB);

    private static async Task<Seed> SeedTechnicianWithTwoTicketsAsync(string marker)
    {
        await using var owner = OwnerContext();
        var role = await owner.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Tecnico) ?? TestEntities.Role(RoleNames.Tecnico);
        if (owner.Entry(role).State == EntityState.Detached) owner.Add(role);
        var user = TestEntities.User(role);
        var tech = TestEntities.Technician(user, isActive: true, status: TechnicianStatus.Disponible);
        ServiceTicket Ticket() => new()
        {
            ClientLocationId = LocationA, ClientId = RlsFixture.ClientA, ReportedByUserId = user.Id,
            Description = marker, Status = ServiceTicketStatus.Asignado, Priority = ServiceTicketPriority.Media, TechnicianId = tech.Id
        };
        var a = Ticket();
        var b = Ticket();
        owner.AddRange(user, tech, a, b);
        await owner.SaveChangesAsync();
        return new Seed(user.Id, tech.Id, a.Id, b.Id);
    }

    private static async Task CleanupAsync(Guid? userId, Guid? technicianId, string marker)
    {
        await using var cleanup = OwnerContext();
        if (technicianId is { } t)
        {
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"TimeLogs\" WHERE \"TechnicianId\" = {0}", t);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"TechnicianAvailabilities\" WHERE \"TechnicianId\" = {0}", t);
        }
        await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"ServiceTickets\" WHERE \"Description\" = {0}", marker);
        if (technicianId is { } t2) await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Technicians\" WHERE \"Id\" = {0}", t2);
        if (userId is { } u) await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Id\" = {0}", u);
    }

    private static async Task<int> OpenLogsAsync(Guid technicianId)
    {
        await using var owner = OwnerContext();
        return await owner.TimeLogs.CountAsync(tl => tl.TechnicianId == technicianId && tl.EndTime == null);
    }

    [PostgresFact]
    public async Task DobleCheckIn_DelMismoTecnico_EnCarrera_SoloUnaVisitaQuedaAbierta()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-H05-" + Guid.NewGuid().ToString("N")[..8];
        Seed? seed = null;
        try
        {
            seed = await SeedTechnicianWithTwoTicketsAsync(marker);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);

            // Petición A: check-in completo en el ticket A, ejecutado mientras B está "pausada".
            async Task RunA()
            {
                await using var dbA = AppContext(tenant);
                await TestCheckIn.Create(dbA).CheckInAsync(seed.TechnicianId, new CheckInRequest { ServiceTicketId = seed.TicketA });
            }

            // Petición B: ya leyó "Disponible" cuando A termina; sin garantía en la base, abriría otra visita.
            await using var dbB = AppContext(tenant, new PauseAfterTechnicianRead(RunA));
            var serviceB = TestCheckIn.Create(dbB);

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                serviceB.CheckInAsync(seed.TechnicianId, new CheckInRequest { ServiceTicketId = seed.TicketB }));

            Assert.Contains("Ya tienes una visita en curso", ex.Message);
            Assert.Equal(1, await OpenLogsAsync(seed.TechnicianId));

            // La transacción de B se revirtió entera: su ticket sigue Asignado.
            await using var check = OwnerContext();
            Assert.Equal(ServiceTicketStatus.Asignado, (await check.ServiceTickets.SingleAsync(t => t.Id == seed.TicketB)).Status);
        }
        finally
        {
            await CleanupAsync(seed?.UserId, seed?.TechnicianId, marker);
        }
    }

    [PostgresFact]
    public async Task TrasElCheckOut_ElTecnicoPuedeAbrirOtraVisita()
    {
        // El índice es PARCIAL (EndTime IS NULL): cerrar la visita libera al técnico.
        await _fixture.EnsureSeededAsync();
        var marker = "PG-H05R-" + Guid.NewGuid().ToString("N")[..8];
        Seed? seed = null;
        try
        {
            seed = await SeedTechnicianWithTwoTicketsAsync(marker);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);

            await using (var db = AppContext(tenant))
                await TestCheckIn.Create(db).CheckInAsync(seed.TechnicianId, new CheckInRequest { ServiceTicketId = seed.TicketA });
            await using (var db = AppContext(tenant))
                await TestCheckIn.Create(db).CheckOutAsync(seed.TechnicianId, new CheckOutRequest { Resolved = false });
            await using (var db = AppContext(tenant))
                await TestCheckIn.Create(db).CheckInAsync(seed.TechnicianId, new CheckInRequest { ServiceTicketId = seed.TicketB });

            Assert.Equal(1, await OpenLogsAsync(seed.TechnicianId));
        }
        finally
        {
            await CleanupAsync(seed?.UserId, seed?.TechnicianId, marker);
        }
    }

    [PostgresFact]
    public async Task DosTecnicos_LaMismaInstalacion_ElSegundoRecibeConflicto()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-H05A-" + Guid.NewGuid().ToString("N")[..8];
        var userIds = new List<Guid>();
        var techIds = new List<Guid>();
        try
        {
            await using (var owner = OwnerContext())
            {
                var role = await owner.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Tecnico) ?? TestEntities.Role(RoleNames.Tecnico);
                if (owner.Entry(role).State == EntityState.Detached) owner.Add(role);
                for (var i = 0; i < 2; i++)
                {
                    var user = TestEntities.User(role);
                    var tech = TestEntities.Technician(user);
                    owner.AddRange(user, tech);
                    userIds.Add(user.Id);
                    techIds.Add(tech.Id);
                }
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);
            TimeLog Install(Guid technicianId) => new()
            {
                TechnicianId = technicianId, AssetId = RlsFixture.AssetWithoutLocation, StartTime = DateTime.UtcNow, Notes = marker
            };

            // Los dos pasaron ya el AnyAsync en memoria (ninguno veía al otro): solo queda la base.
            await using (var first = AppContext(tenant))
            {
                first.TimeLogs.Add(Install(techIds[0]));
                await first.SaveChangesAsync();
            }

            await using var second = AppContext(tenant);
            second.TimeLogs.Add(Install(techIds[1]));
            var ex = await Assert.ThrowsAsync<ConflictException>(() => second.SaveChangesAsync());

            Assert.Contains("Otro técnico ya está atendiendo esta instalación", ex.Message);
        }
        finally
        {
            await using var cleanup = OwnerContext();
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"TimeLogs\" WHERE \"Notes\" = {0}", marker);
            foreach (var t in techIds) await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Technicians\" WHERE \"Id\" = {0}", t);
            foreach (var u in userIds) await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Id\" = {0}", u);
        }
    }
}
