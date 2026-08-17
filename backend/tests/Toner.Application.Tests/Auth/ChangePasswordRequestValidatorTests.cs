using Toner.Application.Auth.Dtos;
using Toner.Application.Auth.Validators;

namespace Toner.Application.Tests.Auth;

// Verifica el hallazgo #15 de SECURITY_AUDIT.md: la contraseña nueva debe cumplir un mínimo de
// longitud (10, subido de 8) y mezcla de tipos de carácter (mayúscula, minúscula, número) — sin
// exigir símbolo, siguiendo NIST 800-63B.
public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    private static ChangePasswordRequest Request(string newPassword) => new()
    {
        CurrentPassword = "cualquiera",
        NewPassword = newPassword
    };

    [Theory]
    [InlineData("corta1A")]           // < 10 caracteres
    [InlineData("sinmayuscula1")]     // sin mayúscula
    [InlineData("SINMINUSCULA1")]     // sin minúscula
    [InlineData("SinNumeroAqui")]     // sin número
    public void NewPassword_NoCumpleLaPolitica_EsInvalida(string password)
    {
        var result = _validator.Validate(Request(password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NewPassword_CumpleLongitudYMezclaSinSimbolo_EsValida()
    {
        // A propósito sin símbolo: NIST 800-63B no lo exige, y el hallazgo #15 pide no complicar con
        // símbolos obligatorios si el negocio no lo pide.
        var result = _validator.Validate(Request("Passw0rdSegura"));

        Assert.True(result.IsValid);
    }
}
