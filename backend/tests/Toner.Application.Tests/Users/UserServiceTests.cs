using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Users;
using Toner.Application.Users.Dtos;
using Toner.Domain.Common;
using Toner.Infrastructure.Auth;

namespace Toner.Application.Tests.Users;

public class UserServiceTests
{
    private static UserService BuildService(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new BCryptPasswordHasher(), NullLogger<UserService>.Instance);

    [Fact]
    public async Task CreateAsync_AlwaysUsesDefaultPassword_AndRequiresPasswordChange()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var city = TestEntities.City();
        arrangeDb.AddRange(role, city);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.CreateAsync(new CreateUserRequest
        {
            Cedula = "1112223334",
            FullName = "Nuevo Coordinador",
            Phone = "3000000000",
            Address = "Calle 1",
            CityId = city.Id,
            RoleName = RoleNames.Coordinador
        });

        Assert.True(result.MustChangePassword);
        Assert.Equal("1112223334", result.Cedula);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var user = await assertDb.Users.SingleAsync(u => u.Id == result.Id);
        Assert.True(new BCryptPasswordHasher().Verify(PasswordDefaults.DefaultPassword, user.PasswordHash));
    }

    [Fact]
    public async Task CreateAsync_DuplicateCedula_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var city = TestEntities.City();
        var existingUser = TestEntities.User(role, cedula: "5556667778");
        arrangeDb.AddRange(role, city, existingUser);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateUserRequest
        {
            Cedula = "5556667778",
            FullName = "Otro Usuario",
            Phone = "3000000001",
            Address = "Calle 2",
            CityId = city.Id,
            RoleName = RoleNames.Coordinador
        }));
    }

    [Fact]
    public async Task ResetPasswordAsync_RehashesToDefaultAndReactivatesMustChangeFlag()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role);
        user.PasswordHash = new BCryptPasswordHasher().Hash("UnaContraseñaQueElUsuarioYaCambio1!");
        user.MustChangePassword = false;
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.ResetPasswordAsync(user.Id);

        Assert.True(result.MustChangePassword);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updated = await assertDb.Users.SingleAsync(u => u.Id == user.Id);
        Assert.True(new BCryptPasswordHasher().Verify(PasswordDefaults.DefaultPassword, updated.PasswordHash));
        Assert.True(updated.MustChangePassword);
    }
}
