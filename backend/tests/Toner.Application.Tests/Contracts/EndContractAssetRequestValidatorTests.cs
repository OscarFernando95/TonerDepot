using Toner.Application.Contracts.Dtos;
using Toner.Application.Contracts.Validators;

namespace Toner.Application.Tests.Contracts;

// Finalizar el vínculo exige un destino explícito (Bodega o Dado de baja) y un motivo: el activo nunca se
// queda "Instalado" sin contrato ni salta a otro estado. La validación vive en el servidor — la del
// frontend no cuenta.
public class EndContractAssetRequestValidatorTests
{
    private readonly EndContractAssetRequestValidator _validator = new();

    private static EndContractAssetRequest Request(string reason, string status) =>
        new() { Reason = reason, NewAssetStatus = status };

    [Theory]
    [InlineData("EnBodega")]
    [InlineData("DadoDeBaja")]
    public void DestinoPermitido_ConMotivo_EsValido(string status)
    {
        Assert.True(_validator.Validate(Request("Cliente devolvió el equipo", status)).IsValid);
    }

    [Theory]
    [InlineData("Instalado")]
    [InlineData("EnMantenimiento")]
    [InlineData("PendienteInstalacion")]
    [InlineData("enbodega")]      // el valor es sensible a mayúsculas: no se normaliza
    [InlineData("NoExiste")]
    [InlineData("")]
    public void DestinoNoPermitido_EsInvalido(string status)
    {
        Assert.False(_validator.Validate(Request("motivo", status)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MotivoVacio_EsInvalido(string reason)
    {
        Assert.False(_validator.Validate(Request(reason, "EnBodega")).IsValid);
    }

    [Fact]
    public void MotivoDeMasDe500Caracteres_EsInvalido()
    {
        Assert.False(_validator.Validate(Request(new string('x', 501), "EnBodega")).IsValid);
    }
}
