using Microsoft.Extensions.Configuration;
using Toner.Application.Calendar;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Calendar;

public class WorkCalendarTests
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly Guid Tech = Guid.NewGuid();

    private static DateTime Utc(int y, int m, int d, int h, int min = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, m, d, h, min, 0, DateTimeKind.Unspecified), Bogota);

    private static List<WorkInterval> WeekdaysNineToFive() =>
        new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Select(day => new WorkInterval(day, new TimeOnly(8, 0), new TimeOnly(17, 0))).ToList();

    private static WorkCalendarContext Context(IEnumerable<TimeOffPeriod>? timeOff = null)
    {
        var offs = timeOff is null
            ? new Dictionary<Guid, IReadOnlyList<TimeOffPeriod>>()
            : new Dictionary<Guid, IReadOnlyList<TimeOffPeriod>> { [Tech] = timeOff.ToList() };

        return new WorkCalendarContext(
            Bogota,
            date => ColombiaHolidays.ForYear(date.Year).ContainsKey(date),
            WeekdaysNineToFive(),
            new Dictionary<Guid, IReadOnlyList<WorkInterval>>(),
            offs);
    }

    [Fact]
    public void ColombiaHolidays_2026_CoincideConElCalendarioOficial()
    {
        var expected = new[]
        {
            (1, 1), (1, 12), (3, 23), (4, 2), (4, 3), (5, 1), (5, 18), (6, 8), (6, 15), (6, 29),
            (7, 20), (8, 7), (8, 17), (10, 12), (11, 2), (11, 16), (12, 8), (12, 25)
        }.Select(x => new DateOnly(2026, x.Item1, x.Item2)).OrderBy(d => d).ToList();

        var actual = ColombiaHolidays.ForYear(2026).Keys.OrderBy(d => d).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ColombiaHolidays_Pascua_2026_EsElCincoDeAbril()
    {
        Assert.Equal(new DateOnly(2026, 4, 5), ColombiaHolidays.EasterSunday(2026));
        Assert.Equal(new DateOnly(2025, 4, 20), ColombiaHolidays.EasterSunday(2025));
    }

    [Fact]
    public void BusinessHours_ViernesTardeALunesMañana_SoloCuentaHorasLaborales()
    {
        // Vie 25-sep 16:00 -> Lun 28-sep 10:00 (sin festivos): 1h del viernes + 2h del lunes.
        var hours = Context().BusinessHours(Tech, SupportCoverage.HorarioOficina, Utc(2026, 9, 25, 16), Utc(2026, 9, 28, 10));

        Assert.Equal(3, hours, 3);
    }

    [Fact]
    public void BusinessHours_SaltaFestivoTrasladadoAlLunes()
    {
        // Lun 17-ago-2026 es festivo (Asunción trasladado): Vie 14 16:00 -> Mar 18 09:00 = 1h + 1h.
        var hours = Context().BusinessHours(Tech, SupportCoverage.HorarioOficina, Utc(2026, 8, 14, 16), Utc(2026, 8, 18, 9));

        Assert.Equal(2, hours, 3);
    }

    [Fact]
    public void BusinessHours_DescuentaElTiempoFueraDeLaOficina()
    {
        // Permiso Lun 09:00 -> Mar 09:00. Vie 16:00 -> Mar 12:00: 1h (vie) + 1h (lun 8-9) + 3h (mar 9-12).
        var off = new TimeOffPeriod(Utc(2026, 9, 28, 9), Utc(2026, 9, 29, 9));

        var hours = Context(new[] { off }).BusinessHours(Tech, SupportCoverage.HorarioOficina, Utc(2026, 9, 25, 16), Utc(2026, 9, 29, 12));

        Assert.Equal(5, hours, 3);
    }

    [Fact]
    public void BusinessHours_Cliente24x7_CuentaHorasCorridas()
    {
        var from = Utc(2026, 9, 26, 22); // sábado de noche
        var to = Utc(2026, 9, 27, 10);   // domingo

        Assert.Equal(12, Context().BusinessHours(Tech, SupportCoverage.Continuo24x7, from, to), 3);
        Assert.Equal(0, Context().BusinessHours(Tech, SupportCoverage.HorarioOficina, from, to), 3);
    }

    [Fact]
    public void IsAssignable_HorarioOficina_ExigeEstarEnHorario_Y24x7SoloExcluyePermisos()
    {
        var saturdayNoon = Utc(2026, 9, 26, 12);
        var mondayNoon = Utc(2026, 9, 28, 12);
        var vacation = new TimeOffPeriod(Utc(2026, 9, 28, 0), Utc(2026, 9, 30, 0));

        Assert.False(Context().IsAssignable(Tech, SupportCoverage.HorarioOficina, saturdayNoon));
        Assert.True(Context().IsAssignable(Tech, SupportCoverage.Continuo24x7, saturdayNoon));
        Assert.True(Context().IsAssignable(Tech, SupportCoverage.HorarioOficina, mondayNoon));
        Assert.False(Context(new[] { vacation }).IsAssignable(Tech, SupportCoverage.HorarioOficina, mondayNoon));
        Assert.False(Context(new[] { vacation }).IsAssignable(Tech, SupportCoverage.Continuo24x7, mondayNoon));
    }

    [Fact]
    public void IsWorking_FueraDelTramo_EsFalso_YRespetaTramosPartidos()
    {
        var split = new Dictionary<Guid, IReadOnlyList<WorkInterval>>
        {
            [Tech] = new List<WorkInterval>
            {
                new(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
                new(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0))
            }
        };
        var ctx = new WorkCalendarContext(Bogota, _ => false, WeekdaysNineToFive(), split, new Dictionary<Guid, IReadOnlyList<TimeOffPeriod>>());

        Assert.True(ctx.IsWorking(Tech, Utc(2026, 9, 28, 9)));
        Assert.False(ctx.IsWorking(Tech, Utc(2026, 9, 28, 13)));
        Assert.True(ctx.IsWorking(Tech, Utc(2026, 9, 28, 17)));
        Assert.False(ctx.IsWorking(Tech, Utc(2026, 9, 29, 9)));
    }

    [Fact]
    public void OpcionesConfiguradasDesdeJson_NoDuplicanLosDiasLaborales()
    {
        // El binder de Microsoft.Extensions.Configuration AGREGA a un arreglo ya inicializado: con el valor
        // inicial {1..5} y el JSON [1..5] quedaban 10 tramos. Este test fija el comportamiento correcto.
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(Enumerable.Range(0, 5).ToDictionary(i => $"WorkCalendar:DefaultWorkDays:{i}", i => (string?)(i + 1).ToString()))
            .Build();
        var options = config.GetSection("WorkCalendar").Get<WorkCalendarOptions>()!;

        var service = new WorkCalendarService(null!, Microsoft.Extensions.Options.Options.Create(options));

        Assert.Equal(5, service.DefaultIntervals.Count);
        Assert.Equal(5, new WorkCalendarService(null!, Microsoft.Extensions.Options.Options.Create(new WorkCalendarOptions())).DefaultIntervals.Count);
    }
}
