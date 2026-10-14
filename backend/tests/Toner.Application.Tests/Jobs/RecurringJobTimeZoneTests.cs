using Toner.Infrastructure.Jobs;

namespace Toner.Application.Tests.Jobs;

// AUDIT.md H-26.
public class RecurringJobTimeZoneTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinValor_UsaBogota(string? id)
    {
        var tz = RecurringJobTimeZone.Resolve(id);

        Assert.Equal("America/Bogota", tz.Id);
        // Bogotá es UTC-5 todo el año: 00:00 local = 05:00 UTC (y no 00:00 UTC = 19:00 local).
        Assert.Equal(TimeSpan.FromHours(-5), tz.BaseUtcOffset);
    }

    [Fact]
    public void IdValido_SeRespeta() =>
        Assert.Equal("America/Bogota", RecurringJobTimeZone.Resolve("America/Bogota").Id);

    [Fact]
    public void IdInexistente_FallaRapidoConMensajeClaro()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RecurringJobTimeZone.Resolve("Marte/Olympus"));

        Assert.Contains("WorkCalendar:TimeZoneId", ex.Message);
        Assert.Contains("Marte/Olympus", ex.Message);
    }
}
