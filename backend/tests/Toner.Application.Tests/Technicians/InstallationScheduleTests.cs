using Toner.Application.Common.Exceptions;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Technicians;

// Una instalación es de lista abierta (no pasa por el motor de asignación), así que el horario del técnico se
// controla al hacer check-in y se anticipa en el listado de pendientes (CanStartNow).
public class InstallationScheduleTests
{
    // FixedTimeProvider.WorkingHours = miércoles 10:00 Bogotá; Weekend = sábado 10:00 Bogotá.
    private sealed record Scenario(string DbName, Guid TechnicianId, Guid AssetId, Guid UserId);

    private static async Task<Scenario> SeedAsync(SupportCoverage coverage, DateTime? timeOffFrom = null, DateTime? timeOffTo = null)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        client.SupportCoverage = coverage;
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        db.AddRange(city, client, location, brand, model, asset, techRole, techUser, technician);
        db.Add(new TechnicianCoverage { TechnicianId = technician.Id, CityId = city.Id });
        if (timeOffFrom is not null)
        {
            db.Add(new TechnicianTimeOff
            {
                TechnicianId = technician.Id, StartsAt = timeOffFrom.Value, EndsAt = timeOffTo!.Value, CreatedByUserId = techUser.Id
            });
        }

        await db.SaveChangesAsync();
        return new Scenario(dbName, technician.Id, asset.Id, techUser.Id);
    }

    private static Task CheckInAsync(Scenario s, TimeProvider time)
    {
        var db = TonerTestDb.CreateContext(s.DbName);
        return TestCheckIn.Create(db, time: time).CheckInAsync(s.TechnicianId, new CheckInRequest { AssetId = s.AssetId });
    }

    [Fact]
    public async Task CheckIn_EnHorarioHabil_Funciona()
    {
        var s = await SeedAsync(SupportCoverage.HorarioOficina);

        await CheckInAsync(s, FixedTimeProvider.WorkingHours);
    }

    [Fact]
    public async Task CheckIn_UnSabado_ClienteDeHorarioOficina_SeRechaza()
    {
        var s = await SeedAsync(SupportCoverage.HorarioOficina);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => CheckInAsync(s, FixedTimeProvider.Weekend));
        Assert.Contains("horario laboral", ex.Message);
    }

    [Fact]
    public async Task CheckIn_UnSabado_ClienteContinuo24x7_Funciona()
    {
        var s = await SeedAsync(SupportCoverage.Continuo24x7);

        await CheckInAsync(s, FixedTimeProvider.Weekend);
    }

    [Theory]
    [InlineData(SupportCoverage.HorarioOficina)]
    [InlineData(SupportCoverage.Continuo24x7)]
    public async Task CheckIn_ConPermisoVigente_SeRechaza_AunQueElClienteSea24x7(SupportCoverage coverage)
    {
        var now = FixedTimeProvider.WorkingHours.GetUtcNow().UtcDateTime;
        var s = await SeedAsync(coverage, now.AddHours(-2), now.AddDays(2));

        await Assert.ThrowsAsync<ConflictException>(() => CheckInAsync(s, FixedTimeProvider.WorkingHours));
    }

    [Fact]
    public async Task CheckIn_RechazadoPorHorario_NoDejaAlTecnicoOcupadoNiAbreVisita()
    {
        var s = await SeedAsync(SupportCoverage.HorarioOficina);

        await Assert.ThrowsAsync<ConflictException>(() => CheckInAsync(s, FixedTimeProvider.Weekend));

        using var db = TonerTestDb.CreateContext(s.DbName);
        Assert.Empty(db.TimeLogs);
        Assert.Equal(TechnicianStatus.Disponible, db.Technicians.Single().Status);
    }

    [Theory]
    [InlineData(SupportCoverage.HorarioOficina, true, true)]
    [InlineData(SupportCoverage.HorarioOficina, false, false)]
    [InlineData(SupportCoverage.Continuo24x7, false, true)]
    public async Task ListarPendientes_MarcaCuálesSePuedenIniciarAhora(SupportCoverage coverage, bool workingHours, bool expected)
    {
        var s = await SeedAsync(coverage);
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = TestCheckIn.Create(db, time: workingHours ? FixedTimeProvider.WorkingHours : FixedTimeProvider.Weekend);

        var page = await service.ListPendingInstallationsAsync(s.TechnicianId, null, null);

        Assert.Equal(expected, Assert.Single(page.Items).CanStartNow);
    }
}
