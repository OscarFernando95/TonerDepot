using Microsoft.EntityFrameworkCore;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Users.Dtos;
using Toner.Application.Users.Validators;
using Toner.Domain.Common;

namespace Toner.Application.Tests.Users;

// AUDIT.md H-34: Ventas es un rol sembrado pero sin ningún endpoint: un usuario creado con él entra y no
// puede hacer nada. No debe poder darse de alta hasta que exista el módulo.
public class CreateUserRequestValidatorTests
{
    private static async Task<(CreateUserRequestValidator Validator, Guid CityId)> BuildAsync()
    {
        var db = TonerTestDb.CreateContext(Guid.NewGuid().ToString());
        var city = TestEntities.City();
        db.Add(city);
        await db.SaveChangesAsync();
        return (new CreateUserRequestValidator(db), city.Id);
    }

    private static CreateUserRequest Request(string role, Guid cityId) => new()
    {
        Cedula = "1234567890",
        FullName = "Usuario Prueba",
        Phone = "3001234567",
        Address = "Calle 1 # 2-3",
        CityId = cityId,
        RoleName = role
    };

    [Fact]
    public async Task RolVentas_NoSePuedeDarDeAlta()
    {
        var (validator, cityId) = await BuildAsync();

        var result = await validator.ValidateAsync(Request(RoleNames.Ventas, cityId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.RoleName));
    }

    [Theory]
    [InlineData(RoleNames.Administrador)]
    [InlineData(RoleNames.Coordinador)]
    [InlineData(RoleNames.Tecnico)]
    public async Task RolesConFuncionalidad_SiSePuedenDarDeAlta(string role)
    {
        var (validator, cityId) = await BuildAsync();

        var result = await validator.ValidateAsync(Request(role, cityId));

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.RoleName));
    }

    [Fact]
    public void Ventas_SigueExistiendoComoRolSembrado()
    {
        // El seeder usa RoleNames.All: quitar Ventas de ahí dejaría sin rol a los usuarios que ya lo tengan.
        Assert.Contains(RoleNames.Ventas, RoleNames.All);
        Assert.DoesNotContain(RoleNames.Ventas, RoleNames.AssignableOnCreate);
    }
}
