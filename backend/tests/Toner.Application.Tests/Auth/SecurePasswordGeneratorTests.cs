using Toner.Application.Auth;
using Toner.Application.Auth.Dtos;
using Toner.Application.Auth.Validators;

namespace Toner.Application.Tests.Auth;

// No duplica las reglas de ChangePasswordRequestValidator: instancia el validador REAL y le hace
// pasar muestras generadas. Si algún día se desincronizan (alguien sube el mínimo, agrega un símbolo
// obligatorio, etc.), este test se pone rojo.
public class SecurePasswordGeneratorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    [Fact]
    public void Generate_SiempreCumpleLaPoliticaRealDelValidador()
    {
        for (var i = 0; i < 200; i++)
        {
            var password = SecurePasswordGenerator.Generate();

            var result = _validator.Validate(new ChangePasswordRequest
            {
                CurrentPassword = "cualquiera",
                NewPassword = password
            });

            Assert.True(result.IsValid, $"'{password}' no cumple la política: {string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
        }
    }

    [Fact]
    public void Generate_NoRepiteContraseñasEntreLlamadas()
    {
        var passwords = Enumerable.Range(0, 200).Select(_ => SecurePasswordGenerator.Generate()).ToList();

        Assert.Equal(passwords.Count, passwords.Distinct().Count());
    }
}
