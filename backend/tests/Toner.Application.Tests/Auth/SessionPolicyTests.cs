using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toner.Application.Auth;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Users;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Infrastructure.Auth;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Auth;

// Política de sesiones: técnico = sesión única (gana la primera); demás roles = varias sesiones con
// timeout por inactividad; logout / cambio de contraseña / cierre por admin revocan de verdad.
public class SessionPolicyTests
{
    private const string Password = "Password123!";
    private static readonly BCryptPasswordHasher Hasher = new();

    private static AuthService BuildAuth(TonerDbContext db) =>
        new(db, Hasher, new JwtTokenGenerator(Options.Create(new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-at-least-32-characters-long",
            ExpiryMinutes = 60
        })), NullLogger<AuthService>.Instance, TestSessions.Create(db));

    private static async Task<(string DbName, Guid UserId)> SeedUserAsync(string roleName, string cedula)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(roleName);
        var user = TestEntities.User(role, cedula: cedula);
        user.PasswordHash = Hasher.Hash(Password);
        db.AddRange(role, user);
        await db.SaveChangesAsync();
        return (dbName, user.Id);
    }

    private static async Task<LoginResult> LoginAsync(string dbName, string cedula, string clientType = "web", string? deviceId = null)
    {
        using var db = TonerTestDb.CreateContext(dbName);
        return await BuildAuth(db).LoginAsync(
            new LoginRequest { Cedula = cedula, Password = Password }, "10.0.0.1", clientType: clientType, userAgent: "test-agent", deviceId: deviceId);
    }

    [Fact]
    public async Task Tecnico_PrimerLogin_CreaSesionUnicaSinTimeoutPorInactividad()
    {
        var (dbName, userId) = await SeedUserAsync(RoleNames.Tecnico, "1001");

        var result = await LoginAsync(dbName, "1001", "mobile");

        Assert.True(result.Succeeded);
        using var db = TonerTestDb.CreateContext(dbName);
        var session = await db.UserSessions.SingleAsync();
        Assert.Equal(userId, session.UserId);
        Assert.Equal("mobile", session.ClientType);
        Assert.True(session.EnforcesSingleSession);
        Assert.Null(session.IdleTimeoutMinutes);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public async Task Tecnico_SegundoLoginConSesionActiva_SeRechazaYLaPrimeraSigueViva()
    {
        var (dbName, _) = await SeedUserAsync(RoleNames.Tecnico, "1002");
        await LoginAsync(dbName, "1002", "mobile");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => LoginAsync(dbName, "1002", "web"));

        Assert.Contains("la app móvil", ex.Message);
        using var db = TonerTestDb.CreateContext(dbName);
        var session = await db.UserSessions.SingleAsync();
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public async Task Tecnico_ReinstalaLaAppEnElMismoTelefono_ReemplazaSuSesionYQuedaLaAuditoria()
    {
        var (dbName, _) = await SeedUserAsync(RoleNames.Tecnico, "1010");
        await LoginAsync(dbName, "1010", "mobile", deviceId: "android-id-abc");

        var result = await LoginAsync(dbName, "1010", "mobile", deviceId: "android-id-abc");

        Assert.True(result.Succeeded);
        using var db = TonerTestDb.CreateContext(dbName);
        var sessions = await db.UserSessions.OrderBy(s => s.IssuedAt).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Equal(SessionService.ReplacedBySameDevice, sessions[0].RevokedReason);
        Assert.NotNull(sessions[0].RevokedAt);
        Assert.Null(sessions[1].RevokedAt);
        // El identificador nunca se guarda en claro.
        Assert.DoesNotContain("android-id-abc", sessions[1].DeviceIdHash);
        Assert.Equal(64, sessions[1].DeviceIdHash!.Length);
    }

    [Fact]
    public async Task Tecnico_OtroTelefono_SigueBloqueadoPorLaSesionActiva()
    {
        var (dbName, _) = await SeedUserAsync(RoleNames.Tecnico, "1011");
        await LoginAsync(dbName, "1011", "mobile", deviceId: "android-id-abc");

        await Assert.ThrowsAsync<ConflictException>(() => LoginAsync(dbName, "1011", "mobile", deviceId: "android-id-otro"));

        using var db = TonerTestDb.CreateContext(dbName);
        Assert.Null((await db.UserSessions.SingleAsync()).RevokedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Tecnico_SinIdentificadorDeDispositivo_NuncaReemplazaLaSesion(string? deviceId)
    {
        var (dbName, _) = await SeedUserAsync(RoleNames.Tecnico, "1012");
        await LoginAsync(dbName, "1012", "mobile", deviceId: deviceId);

        // Ni la web ni una app sin id pueden aprovechar el reemplazo: dos sesiones "sin dispositivo" no son el mismo.
        await Assert.ThrowsAsync<ConflictException>(() => LoginAsync(dbName, "1012", "mobile", deviceId: deviceId));
        await Assert.ThrowsAsync<ConflictException>(() => LoginAsync(dbName, "1012", "web"));
    }

    [Fact]
    public async Task Tecnico_UnIdentificadorExageradamenteLargo_SeIgnora()
    {
        var (dbName, _) = await SeedUserAsync(RoleNames.Tecnico, "1013");
        var huge = new string('x', 500);
        await LoginAsync(dbName, "1013", "mobile", deviceId: huge);

        await Assert.ThrowsAsync<ConflictException>(() => LoginAsync(dbName, "1013", "mobile", deviceId: huge));
        using var db = TonerTestDb.CreateContext(dbName);
        Assert.Null((await db.UserSessions.SingleAsync()).DeviceIdHash);
    }

    [Fact]
    public async Task Tecnico_TrasLogout_PuedeIniciarSesionDesdeOtroCliente()
    {
        var (dbName, userId) = await SeedUserAsync(RoleNames.Tecnico, "1003");
        await LoginAsync(dbName, "1003", "mobile");

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var sessionId = (await db.UserSessions.SingleAsync()).Id;
            await BuildAuth(db).LogoutAsync(userId, sessionId);
        }

        var result = await LoginAsync(dbName, "1003", "web");

        Assert.True(result.Succeeded);
        using var checkDb = TonerTestDb.CreateContext(dbName);
        Assert.Equal(2, await checkDb.UserSessions.CountAsync());
        Assert.Equal(1, await checkDb.UserSessions.CountAsync(s => s.RevokedAt == null));
    }

    [Fact]
    public async Task Tecnico_SesionAnteriorVencidaPorTiempo_NoBloqueaElLogin()
    {
        var (dbName, _) = await SeedUserAsync(RoleNames.Tecnico, "1004");
        await LoginAsync(dbName, "1004", "mobile");

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var session = await db.UserSessions.SingleAsync();
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var result = await LoginAsync(dbName, "1004", "web");

        Assert.True(result.Succeeded);
        using var checkDb = TonerTestDb.CreateContext(dbName);
        Assert.Contains(await checkDb.UserSessions.ToListAsync(), s => s.RevokedReason == "Expirada");
    }

    [Fact]
    public async Task Coordinador_PuedeTenerVariasSesiones_ConTimeoutPorInactividad()
    {
        var (dbName, _) = await SeedUserAsync(RoleNames.Coordinador, "2001");

        await LoginAsync(dbName, "2001", "web");
        await LoginAsync(dbName, "2001", "mobile");

        using var db = TonerTestDb.CreateContext(dbName);
        var sessions = await db.UserSessions.ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s =>
        {
            Assert.False(s.EnforcesSingleSession);
            Assert.Equal(30, s.IdleTimeoutMinutes);
        });
    }

    [Fact]
    public async Task ChangePassword_RevocaLasSesionesDelUsuario()
    {
        var (dbName, userId) = await SeedUserAsync(RoleNames.Tecnico, "1005");
        await LoginAsync(dbName, "1005");

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            await BuildAuth(db).ChangePasswordAsync(userId, new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "NuevaClave123!" });
        }

        using var checkDb = TonerTestDb.CreateContext(dbName);
        var session = await checkDb.UserSessions.SingleAsync();
        Assert.NotNull(session.RevokedAt);
        Assert.Equal("CambioContraseña", session.RevokedReason);
    }

    [Fact]
    public async Task AdminRevocaSesiones_ElTecnicoVuelveAPoderEntrar()
    {
        var (dbName, userId) = await SeedUserAsync(RoleNames.Tecnico, "1006");
        await LoginAsync(dbName, "1006", "mobile");

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var users = new UserService(db, Hasher, new FakeBackgroundJobScheduler(), NullLogger<UserService>.Instance, TestSessions.Create(db));
            var revoked = await users.RevokeSessionsAsync(userId, Guid.NewGuid());
            Assert.Equal(1, revoked);
        }

        Assert.True((await LoginAsync(dbName, "1006", "web")).Succeeded);
    }

    [Fact]
    public async Task Validate_SesionSinActividadMasDelLimite_SeCierraPorInactividad()
    {
        var (dbName, userId) = await SeedUserAsync(RoleNames.Coordinador, "2002");
        await LoginAsync(dbName, "2002");

        using var db = TonerTestDb.CreateContext(dbName);
        var session = await db.UserSessions.SingleAsync();
        session.LastSeenAt = DateTime.UtcNow.AddMinutes(-31);
        await db.SaveChangesAsync();

        var result = await TestSessions.Create(db).ValidateAsync(session.Id, userId);

        Assert.Equal(SessionValidationResult.IdleTimeout, result);
        using var checkDb = TonerTestDb.CreateContext(dbName);
        Assert.Equal("Inactividad", (await checkDb.UserSessions.SingleAsync()).RevokedReason);
    }

    [Fact]
    public async Task Validate_ConActividadReciente_RefrescaLastSeenSoloTrasElIntervalo()
    {
        var (dbName, userId) = await SeedUserAsync(RoleNames.Coordinador, "2003");
        await LoginAsync(dbName, "2003");

        using var db = TonerTestDb.CreateContext(dbName);
        var session = await db.UserSessions.SingleAsync();
        var original = session.LastSeenAt;

        Assert.Equal(SessionValidationResult.Valid, await TestSessions.Create(db).ValidateAsync(session.Id, userId));
        Assert.Equal(original, (await TonerTestDb.CreateContext(dbName).UserSessions.SingleAsync()).LastSeenAt);

        session.LastSeenAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();
        Assert.Equal(SessionValidationResult.Valid, await TestSessions.Create(db).ValidateAsync(session.Id, userId));
        Assert.True((await TonerTestDb.CreateContext(dbName).UserSessions.SingleAsync()).LastSeenAt > DateTime.UtcNow.AddMinutes(-1));
    }
}
