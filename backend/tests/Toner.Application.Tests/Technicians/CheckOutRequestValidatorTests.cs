using FluentValidation;
using Toner.Application.Inventory.Dtos;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Technicians.Validators;

namespace Toner.Application.Tests.Technicians;

// AUDIT.md H-10.
public class CheckOutRequestValidatorTests
{
    private static readonly CheckOutRequestValidator Validator = new();

    [Fact]
    public void RequestMinimo_EsValido() =>
        Assert.True(Validator.Validate(new CheckOutRequest()).IsValid);

    [Fact]
    public void RequestCompletoValido_EsValido()
    {
        var request = new CheckOutRequest
        {
            Notes = new string('n', 1000),
            Area = new string('a', 150),
            Latitude = -90, Longitude = 180, AccuracyMeters = 0,
            InitialCounterValue = 0, ExistingConsumablesPrints = 0, ExternalAssetCounter = 0,
            ExternalAssetBrand = new string('b', 100), ExternalAssetModel = new string('m', 150),
            Parts = new[] { new UsedPartRequest { ItemId = Guid.NewGuid(), Quantity = 100 } }
        };
        Assert.True(Validator.Validate(request).IsValid);
    }

    public static IEnumerable<object[]> Invalidos() => new[]
    {
        new object[] { "Notes", new CheckOutRequest { Notes = new string('x', 1001) } },
        new object[] { "Area", new CheckOutRequest { Area = new string('x', 151) } },
        new object[] { "ExternalAssetBrand", new CheckOutRequest { ExternalAssetBrand = new string('x', 101) } },
        new object[] { "ExternalAssetModel", new CheckOutRequest { ExternalAssetModel = new string('x', 151) } },
        new object[] { "Latitude", new CheckOutRequest { Latitude = 90.0001 } },
        new object[] { "Latitude", new CheckOutRequest { Latitude = -91 } },
        new object[] { "Longitude", new CheckOutRequest { Longitude = 180.5 } },
        new object[] { "Longitude", new CheckOutRequest { Longitude = -181 } },
        new object[] { "AccuracyMeters", new CheckOutRequest { AccuracyMeters = -1 } },
        new object[] { "InitialCounterValue", new CheckOutRequest { InitialCounterValue = -1 } },
        new object[] { "ExistingConsumablesPrints", new CheckOutRequest { ExistingConsumablesPrints = -5 } },
        new object[] { "ExternalAssetCounter", new CheckOutRequest { ExternalAssetCounter = -1 } },
        new object[] { "Parts[0].Quantity", new CheckOutRequest { Parts = new[] { new UsedPartRequest { ItemId = Guid.NewGuid(), Quantity = 0 } } } },
        new object[] { "Parts[0].Quantity", new CheckOutRequest { Parts = new[] { new UsedPartRequest { ItemId = Guid.NewGuid(), Quantity = 101 } } } },
        new object[] { "Parts[0].ItemId", new CheckOutRequest { Parts = new[] { new UsedPartRequest { ItemId = Guid.Empty, Quantity = 1 } } } },
        new object[] { "Parts", new CheckOutRequest { Parts = Enumerable.Range(0, 101).Select(_ => new UsedPartRequest { ItemId = Guid.NewGuid(), Quantity = 1 }).ToArray() } },
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public void ValorFueraDeRango_Falla_SobreLaPropiedadCorrecta(string propiedad, CheckOutRequest request)
    {
        var result = Validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == propiedad);
    }

    [Fact]
    public void MarcaYModeloSeMidenYaRecortados()
    {
        // El servicio hace Trim() antes de guardar: espacios de relleno no deben provocar un rechazo.
        var request = new CheckOutRequest { ExternalAssetBrand = new string('b', 100) + "   ", ExternalAssetModel = "  " + new string('m', 150) };
        Assert.True(Validator.Validate(request).IsValid);
    }
}
