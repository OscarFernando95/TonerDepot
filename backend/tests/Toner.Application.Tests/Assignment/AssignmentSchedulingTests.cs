using Microsoft.EntityFrameworkCore;
using Toner.Application.Assignment;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Assignment;

// Horario laboral, festivos, "fuera de la oficina" y cobertura del cliente en la asignación automática.
public class AssignmentSchedulingTests
{
    private sealed record Scenario(string DbName, Guid TicketId, Guid TechnicianId);

    private static async Task<Scenario> SeedAsync(
        SupportCoverage coverage, Action<Technician, List<object>>? customize = null)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        client.SupportCoverage = coverage;
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user);
        ticket.ClientId = client.Id;

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);
        var cover = TestEntities.Coverage(technician, city);

        var extras = new List<object>();
        customize?.Invoke(technician, extras);

        db.AddRange(role, user, city, client, location, ticket, techRole, techUser, technician, cover);
        db.AddRange(extras);
        await db.SaveChangesAsync();
        return new Scenario(dbName, ticket.Id, technician.Id);
    }

    private static async Task<(Guid? Result, ServiceTicket Ticket, List<AssignmentHistory> History)> AssignAsync(
        Scenario scenario, TimeProvider time, bool record = true)
    {
        using (var actDb = TonerTestDb.CreateContext(scenario.DbName))
        {
            var ticket = await actDb.ServiceTickets.SingleAsync(t => t.Id == scenario.TicketId);
            var result = await TestAssignment.Create(actDb, time).AssignServiceTicketAsync(ticket, default, record);
            await actDb.SaveChangesAsync();
        }

        using var assertDb = TonerTestDb.CreateContext(scenario.DbName);
        var stored = await assertDb.ServiceTickets.SingleAsync(t => t.Id == scenario.TicketId);
        var history = await assertDb.AssignmentHistories.Where(h => h.ServiceTicketId == scenario.TicketId).ToListAsync();
        return (stored.TechnicianId, stored, history);
    }

    [Fact]
    public async Task ClienteHorarioOficina_FinDeSemana_NoSeAsigna_YDejaMotivoDeHorario()
    {
        var scenario = await SeedAsync(SupportCoverage.HorarioOficina);

        var (assigned, ticket, history) = await AssignAsync(scenario, FixedTimeProvider.Weekend);

        Assert.Null(assigned);
        Assert.Equal(ServiceTicketStatus.SinAsignar, ticket.Status);
        Assert.Contains("horario", history.Single().Reason);
    }

    [Fact]
    public async Task ClienteHorarioOficina_EnHorarioLaboral_SeAsigna()
    {
        var scenario = await SeedAsync(SupportCoverage.HorarioOficina);

        var (assigned, _, _) = await AssignAsync(scenario, FixedTimeProvider.WorkingHours);

        Assert.Equal(scenario.TechnicianId, assigned);
    }

    [Fact]
    public async Task Cliente24x7_FinDeSemana_SeAsigna_IgnorandoElHorario()
    {
        var scenario = await SeedAsync(SupportCoverage.Continuo24x7);

        var (assigned, _, _) = await AssignAsync(scenario, FixedTimeProvider.Weekend);

        Assert.Equal(scenario.TechnicianId, assigned);
    }

    [Theory]
    [InlineData(SupportCoverage.HorarioOficina)]
    [InlineData(SupportCoverage.Continuo24x7)]
    public async Task TecnicoFueraDeLaOficina_NoSeAsigna_NiSiquieraEn24x7(SupportCoverage coverage)
    {
        var now = FixedTimeProvider.WorkingHours.GetUtcNow().UtcDateTime;
        var scenario = await SeedAsync(coverage, (technician, extras) => extras.Add(new TechnicianTimeOff
        {
            TechnicianId = technician.Id,
            StartsAt = now.AddDays(-1),
            EndsAt = now.AddDays(2),
            CreatedByUserId = Guid.NewGuid()
        }));

        var (assigned, _, _) = await AssignAsync(scenario, FixedTimeProvider.WorkingHours);

        Assert.Null(assigned);
    }

    [Fact]
    public async Task FueraDeOficinaCancelado_YaNoExcluyeAlTecnico()
    {
        var now = FixedTimeProvider.WorkingHours.GetUtcNow().UtcDateTime;
        var scenario = await SeedAsync(SupportCoverage.HorarioOficina, (technician, extras) => extras.Add(new TechnicianTimeOff
        {
            TechnicianId = technician.Id,
            StartsAt = now.AddDays(-1),
            EndsAt = now.AddDays(2),
            CancelledAt = now.AddHours(-1),
            CreatedByUserId = Guid.NewGuid()
        }));

        var (assigned, _, _) = await AssignAsync(scenario, FixedTimeProvider.WorkingHours);

        Assert.Equal(scenario.TechnicianId, assigned);
    }

    [Fact]
    public async Task HorarioPropioDelTecnico_ReemplazaAlDeLaEmpresa()
    {
        // Solo trabaja miércoles de 20:00 a 22:00: a las 10:00 del miércoles no está disponible.
        var scenario = await SeedAsync(SupportCoverage.HorarioOficina, (technician, extras) => extras.Add(new TechnicianWorkInterval
        {
            TechnicianId = technician.Id,
            Day = DayOfWeek.Wednesday,
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(22, 0)
        }));

        var (assigned, _, _) = await AssignAsync(scenario, FixedTimeProvider.WorkingHours);

        Assert.Null(assigned);
    }

    [Fact]
    public async Task FestivoLegal_NoSeAsignaAClienteDeOficina()
    {
        // Lunes 2026-08-17 (Asunción trasladado), 10:00 Bogotá.
        var holiday = new FixedTimeProvider(new DateTimeOffset(2026, 8, 17, 15, 0, 0, TimeSpan.Zero));
        var scenario = await SeedAsync(SupportCoverage.HorarioOficina);

        var (assigned, _, _) = await AssignAsync(scenario, holiday);

        Assert.Null(assigned);
    }

    [Fact]
    public async Task IntentoDeReasignacion_SinRegistrarFallo_NoAgregaHistorial()
    {
        var scenario = await SeedAsync(SupportCoverage.HorarioOficina);

        var (assigned, _, history) = await AssignAsync(scenario, FixedTimeProvider.Weekend, record: false);

        Assert.Null(assigned);
        Assert.Empty(history);
    }

    [Fact]
    public async Task PendingAssignmentService_AsignaAlEntrarEnHorario_SinLlenarElHistorialDeFallos()
    {
        var scenario = await SeedAsync(SupportCoverage.HorarioOficina);
        using (var db = TonerTestDb.CreateContext(scenario.DbName))
        {
            (await db.ServiceTickets.SingleAsync()).Status = ServiceTicketStatus.SinAsignar;
            await db.SaveChangesAsync();
        }

        // Fin de semana: no hay a quién asignar y NO se agrega historial por el intento fallido.
        using (var db = TonerTestDb.CreateContext(scenario.DbName))
        {
            var result = await new PendingAssignmentService(db, TestAssignment.Create(db, FixedTimeProvider.Weekend)).RunAsync();
            Assert.Equal((0, 0), result);
        }
        using (var db = TonerTestDb.CreateContext(scenario.DbName))
        {
            Assert.Empty(await db.AssignmentHistories.ToListAsync());
        }

        // Ya en horario laboral: se asigna y queda el registro de la asignación.
        using (var db = TonerTestDb.CreateContext(scenario.DbName))
        {
            var result = await new PendingAssignmentService(db, TestAssignment.Create(db, FixedTimeProvider.WorkingHours)).RunAsync();
            Assert.Equal((1, 0), result);
        }
        using (var db = TonerTestDb.CreateContext(scenario.DbName))
        {
            var ticket = await db.ServiceTickets.SingleAsync();
            Assert.Equal(scenario.TechnicianId, ticket.TechnicianId);
            Assert.Equal(ServiceTicketStatus.Asignado, ticket.Status);
            Assert.Equal(scenario.TechnicianId, (await db.AssignmentHistories.SingleAsync()).TechnicianId);
        }
    }
}
