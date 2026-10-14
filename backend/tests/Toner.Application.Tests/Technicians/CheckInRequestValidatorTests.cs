using FluentValidation;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Technicians.Validators;
using Toner.Application.Tests.TestSupport;

namespace Toner.Application.Tests.Technicians;

// AUDIT.md H-37: el check-in no validaba rangos de ubicación (el check-out sí, desde H-10).
public class CheckInRequestValidatorTests
{
    private static async Task<FluentValidation.Results.ValidationResult> ValidateAsync(CheckInRequest request)
    {
        // Sin ServiceTicketId/AssetId existentes la regla "exactamente uno" o la de existencia también fallan:
        // aquí solo se mira la propiedad bajo prueba.
        using var db = TonerTestDb.CreateContext(Guid.NewGuid().ToString());
        return await new CheckInRequestValidator(db).ValidateAsync(request);
    }

    [Theory]
    [InlineData(90.5, 0, "Latitude")]
    [InlineData(-91, 0, "Latitude")]
    [InlineData(0, 180.1, "Longitude")]
    [InlineData(0, -181, "Longitude")]
    public async Task UbicacionFueraDeRango_Falla(double lat, double lon, string property)
    {
        var result = await ValidateAsync(new CheckInRequest { Latitude = lat, Longitude = lon });
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public async Task PrecisionNegativa_Falla()
    {
        var result = await ValidateAsync(new CheckInRequest { AccuracyMeters = -1 });
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CheckInRequest.AccuracyMeters));
    }

    [Fact]
    public async Task UbicacionValidaOAusente_NoGeneraErroresDeUbicacion()
    {
        foreach (var request in new[] { new CheckInRequest { Latitude = 4.6, Longitude = -74.08, AccuracyMeters = 12 }, new CheckInRequest() })
        {
            var result = await ValidateAsync(request);
            Assert.DoesNotContain(result.Errors, e => e.PropertyName is "Latitude" or "Longitude" or "AccuracyMeters");
        }
    }
}
