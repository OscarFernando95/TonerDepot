using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Toner.Application.Auth.Validators;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Auth.Dtos;
using Toner.Application.Users;
using Toner.Application.Users.Dtos;
using Toner.Domain.Common;
using Toner.Infrastructure.Auth;

namespace Toner.Application.Tests.Users;

public class UserServiceTests
{
    private static UserService BuildService(Infrastructure.Persistence.TonerDbContext db, FakeBackgroundJobScheduler? jobScheduler = null) =>
        new(db, new BCryptPasswordHasher(), jobScheduler ?? new FakeBackgroundJobScheduler(), NullLogger<UserService>.Instance);

    // La contraseña real que cumpla ChangePasswordRequestValidator (no una copia paralela de sus
    // reglas): si el generador y el validador se desincronizan, este assert falla.
    private static void AssertMeetsRealPasswordPolicy(string password)
    {
        var result = new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest
        {
            CurrentPassword = "cualquiera",
            NewPassword = password
        });
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task CreateAsync_GeneratesPasswordMeetingRealPolicy_AndRequiresPasswordChange()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var city = TestEntities.City();
        arrangeDb.AddRange(role, city);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var jobScheduler = new FakeBackgroundJobScheduler();
        var service = BuildService(actDb, jobScheduler);

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
        AssertMeetsRealPasswordPolicy(result.GeneratedPassword);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var user = await assertDb.Users.SingleAsync(u => u.Id == result.Id);
        Assert.True(new BCryptPasswordHasher().Verify(result.GeneratedPassword, user.PasswordHash));

        // El job debe encolarse con la MISMA contraseña que se muestra en pantalla — si no, el
        // correo administrativo y lo que ve el admin quedarían desincronizados.
        var enqueued = Assert.Single(jobScheduler.EnqueuedEmails);
        Assert.Equal("1112223334", enqueued.Cedula);
        Assert.Equal(result.GeneratedPassword, enqueued.GeneratedPassword);
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
    public async Task ResetPasswordAsync_GeneratesPasswordMeetingRealPolicy_AndReactivatesMustChangeFlag()
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
        var jobScheduler = new FakeBackgroundJobScheduler();
        var service = BuildService(actDb, jobScheduler);

        var result = await service.ResetPasswordAsync(user.Id);

        Assert.True(result.MustChangePassword);
        AssertMeetsRealPasswordPolicy(result.GeneratedPassword);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updated = await assertDb.Users.SingleAsync(u => u.Id == user.Id);
        Assert.True(new BCryptPasswordHasher().Verify(result.GeneratedPassword, updated.PasswordHash));
        Assert.True(updated.MustChangePassword);

        var enqueued = Assert.Single(jobScheduler.EnqueuedEmails);
        Assert.Equal(user.Cedula, enqueued.Cedula);
        Assert.Equal(result.GeneratedPassword, enqueued.GeneratedPassword);
    }

    [Fact]
    public async Task ResetPasswordAsync_RegeneratesSecurityStamp()
    {
        // Ver SECURITY_AUDIT.md hallazgo #6: sin esto, un JWT ya emitido seguía siendo válido después
        // de que un admin reseteara la contraseña de un usuario.
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role);
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();
        var stampBefore = user.SecurityStamp;

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await service.ResetPasswordAsync(user.Id);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updated = await assertDb.Users.SingleAsync(u => u.Id == user.Id);
        Assert.NotEqual(stampBefore, updated.SecurityStamp);
    }

    [Fact]
    public async Task SetActiveStatusAsync_RegeneratesSecurityStamp()
    {
        // Ver SECURITY_AUDIT.md hallazgo #6: sin esto, un JWT ya emitido seguía siendo válido después
        // de desactivar (o reactivar) la cuenta de un usuario.
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role);
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();
        var stampBefore = user.SecurityStamp;

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await service.SetActiveStatusAsync(user.Id, isActive: false);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updated = await assertDb.Users.SingleAsync(u => u.Id == user.Id);
        Assert.NotEqual(stampBefore, updated.SecurityStamp);
    }
}
