using Toner.Application.Auth;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Toner.Application.Tests.Auth;

public class AuthServiceTests
{
    private static readonly BCryptPasswordHasher Hasher = new();

    private static AuthService BuildService(Infrastructure.Persistence.TonerDbContext db, Microsoft.Extensions.Logging.ILogger<AuthService>? logger = null) =>
        new(db, Hasher, new JwtTokenGenerator(Options.Create(new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-at-least-32-characters-long",
            ExpiryMinutes = 60
        })), logger ?? NullLogger<AuthService>.Instance);

    [Fact]
    public async Task LoginAsync_ValidCedulaAndPassword_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "1010101010");
        user.PasswordHash = Hasher.Hash("Password123!");
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.LoginAsync(new LoginRequest { Cedula = "1010101010", Password = "Password123!" });

        Assert.True(result.Succeeded);
        Assert.Equal("1010101010", result.User!.Cedula);
    }

    [Fact]
    public async Task LoginAsync_UnknownCedula_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.LoginAsync(new LoginRequest { Cedula = "0000000000", Password = "whatever" });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "2020202020");
        user.PasswordHash = Hasher.Hash("Password123!");
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.LoginAsync(new LoginRequest { Cedula = "2020202020", Password = "wrong-password" });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "3030303030");
        user.PasswordHash = Hasher.Hash("Password123!");
        user.IsActive = false;
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.LoginAsync(new LoginRequest { Cedula = "3030303030", Password = "Password123!" });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_ClearsMustChangePasswordFlag()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role);
        user.PasswordHash = Hasher.Hash(PasswordDefaults.DefaultPassword);
        user.MustChangePassword = true;
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await service.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = PasswordDefaults.DefaultPassword,
            NewPassword = "NuevaContraseñaSegura1!"
        });

        var updated = await service.GetCurrentUserAsync(user.Id);
        Assert.False(updated.MustChangePassword);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsAndLeavesFlagUntouched()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role);
        user.PasswordHash = Hasher.Hash(PasswordDefaults.DefaultPassword);
        user.MustChangePassword = true;
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = "no-es-la-actual",
            NewPassword = "NuevaContraseñaSegura1!"
        }));

        var stillMustChange = await service.GetCurrentUserAsync(user.Id);
        Assert.True(stillMustChange.MustChangePassword);
    }
}
