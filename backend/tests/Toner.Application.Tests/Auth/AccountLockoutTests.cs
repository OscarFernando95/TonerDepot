using Microsoft.EntityFrameworkCore;
using Toner.Application.Auth;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Interfaces;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Toner.Application.Tests.Auth;

// Verifica el hallazgo #9 de SECURITY_AUDIT.md: bloqueo de cuenta tras 5 intentos fallidos
// consecutivos, complementario al rate limiting por IP del hallazgo #2 (protege contra un
// atacante que rota de IP pero insiste sobre la misma cédula) y sin reabrir el oráculo de
// tiempos que cerró el hallazgo #7.
public class AccountLockoutTests
{
    private static readonly BCryptPasswordHasher RealHasher = new();

    private static AuthService BuildService(Infrastructure.Persistence.TonerDbContext db, IPasswordHasher? hasher = null, ILogger<AuthService>? logger = null) =>
        new(db, hasher ?? RealHasher, new JwtTokenGenerator(Options.Create(new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-at-least-32-characters-long",
            ExpiryMinutes = 60
        })), logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);

    [Fact]
    public async Task LoginAsync_CincoIntentosFallidosConsecutivos_BloqueaLaCuenta()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "8080808080");
        user.PasswordHash = RealHasher.Hash("Password123!");
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        for (var i = 0; i < 5; i++)
        {
            using var db = TonerTestDb.CreateContext(dbName);
            var service = BuildService(db);
            var result = await service.LoginAsync(new LoginRequest { Cedula = "8080808080", Password = "incorrecta" });
            Assert.False(result.Succeeded);
        }

        using var checkDb = TonerTestDb.CreateContext(dbName);
        var stored = await checkDb.Users.SingleAsync(u => u.Cedula == "8080808080");
        Assert.Equal(5, stored.FailedLoginAttempts);
        Assert.NotNull(stored.LockedOutUntil);
        Assert.True(stored.LockedOutUntil > DateTime.UtcNow);

        // Ni siquiera la contraseña correcta funciona mientras está bloqueada.
        using var sixthDb = TonerTestDb.CreateContext(dbName);
        var sixthService = BuildService(sixthDb);
        var sixthResult = await sixthService.LoginAsync(new LoginRequest { Cedula = "8080808080", Password = "Password123!" });
        Assert.False(sixthResult.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_ContrasenaCorrectaDuranteBloqueo_RechazadaSinVerificarHashReal()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "9090909090");
        user.PasswordHash = RealHasher.Hash("Password123!");
        user.FailedLoginAttempts = 5;
        user.LockedOutUntil = DateTime.UtcNow.AddMinutes(15);
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var recordingHasher = new RecordingPasswordHasher();
        var service = BuildService(actDb, recordingHasher);

        var result = await service.LoginAsync(new LoginRequest { Cedula = "9090909090", Password = "Password123!" });

        Assert.False(result.Succeeded);
        var hashCompared = Assert.Single(recordingHasher.VerifiedAgainstHashes);
        Assert.NotEqual(user.PasswordHash, hashCompared);
    }

    [Fact]
    public async Task LoginAsync_TiempoDeCuentaBloqueadaEsComparableAlDeCedulaInexistente()
    {
        // Ambas ramas (cuenta bloqueada y cédula inexistente) hacen exactamente un Verify() contra
        // el hash señuelo y ningún SaveChangesAsync — deberían costar prácticamente lo mismo. Cotas
        // amplias en ambas direcciones para no volverse un test frágil por ruido de scheduling.
        var lockedDbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(lockedDbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "1212121212");
        user.PasswordHash = RealHasher.Hash("Password123!");
        user.FailedLoginAttempts = 5;
        user.LockedOutUntil = DateTime.UtcNow.AddMinutes(15);
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        const int iterations = 3;

        var lockedElapsedMs = await MeasureAverageMillisecondsAsync(lockedDbName, "1212121212", "cualquiera", iterations);
        var unknownElapsedMs = await MeasureAverageMillisecondsAsync(Guid.NewGuid().ToString(), "0000000000", "cualquiera", iterations);

        Assert.True(
            lockedElapsedMs >= unknownElapsedMs * 0.3 && lockedElapsedMs <= unknownElapsedMs * 3.0,
            $"El camino de cuenta bloqueada ({lockedElapsedMs:F1}ms) difiere demasiado del de cédula " +
            $"inexistente ({unknownElapsedMs:F1}ms) — ambos deberían costar lo mismo (un solo Verify() " +
            "contra el hash señuelo, sin escritura a BD).");
    }

    [Fact]
    public async Task LoginAsync_LoginExitoso_ReseteaContadorYDesbloqueo()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "1313131313");
        user.PasswordHash = RealHasher.Hash("Password123!");
        user.FailedLoginAttempts = 3;
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.LoginAsync(new LoginRequest { Cedula = "1313131313", Password = "Password123!" });

        Assert.True(result.Succeeded);

        using var checkDb = TonerTestDb.CreateContext(dbName);
        var stored = await checkDb.Users.SingleAsync(u => u.Cedula == "1313131313");
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.Null(stored.LockedOutUntil);
    }

    [Fact]
    public async Task LoginAsync_PasadoElTiempoDeBloqueo_ContrasenaCorrectaVuelveAFuncionar()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "1414141414");
        user.PasswordHash = RealHasher.Hash("Password123!");
        user.FailedLoginAttempts = 5;
        // Bloqueo ya vencido — no se espera en tiempo real, se simula directamente en el dato.
        user.LockedOutUntil = DateTime.UtcNow.AddMinutes(-1);
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.LoginAsync(new LoginRequest { Cedula = "1414141414", Password = "Password123!" });

        Assert.True(result.Succeeded);

        using var checkDb = TonerTestDb.CreateContext(dbName);
        var stored = await checkDb.Users.SingleAsync(u => u.Cedula == "1414141414");
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.Null(stored.LockedOutUntil);
    }

    [Fact]
    public async Task LoginAsync_AlExpirarElBloqueo_ElContadorVuelveACero()
    {
        // SECURITY_AUDIT_V2.md hallazgo N2: antes, FailedLoginAttempts quedaba en 5 después de que
        // expiraba el bloqueo, así que UN solo intento fallido posterior re-bloqueaba la cuenta otros
        // 15 minutos — alguien que conociera la cédula la mantenía bloqueada con 1 request/15 min.
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "1515151515");
        user.PasswordHash = RealHasher.Hash("Password123!");
        user.FailedLoginAttempts = 5;
        user.LockedOutUntil = DateTime.UtcNow.AddMinutes(-1); // bloqueo ya vencido
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        // Un intento fallido tras expirar el bloqueo NO debe re-bloquear: el contador arranca de
        // cero, así que queda en 1 y hacen falta 5 para volver a bloquear.
        var result = await service.LoginAsync(new LoginRequest { Cedula = "1515151515", Password = "incorrecta" });

        Assert.False(result.Succeeded);

        using var checkDb = TonerTestDb.CreateContext(dbName);
        var stored = await checkDb.Users.SingleAsync(u => u.Cedula == "1515151515");
        Assert.Equal(1, stored.FailedLoginAttempts);
        Assert.Null(stored.LockedOutUntil);
    }

    [Fact]
    public async Task LoginAsync_AlExpirarElBloqueo_ExigeCincoFallosNuevosParaVolverABloquear()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "1616161616");
        user.PasswordHash = RealHasher.Hash("Password123!");
        user.FailedLoginAttempts = 5;
        user.LockedOutUntil = DateTime.UtcNow.AddMinutes(-1);
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        // Cuatro fallos tras expirar el bloqueo: todavía NO debe estar bloqueada.
        for (var i = 0; i < 4; i++)
        {
            using var db = TonerTestDb.CreateContext(dbName);
            await BuildService(db).LoginAsync(new LoginRequest { Cedula = "1616161616", Password = "incorrecta" });
        }

        using (var midCheck = TonerTestDb.CreateContext(dbName))
        {
            var midUser = await midCheck.Users.SingleAsync(u => u.Cedula == "1616161616");
            Assert.Equal(4, midUser.FailedLoginAttempts);
            Assert.Null(midUser.LockedOutUntil);
        }

        // La contraseña correcta todavía funciona: la cuenta no quedó bloqueada por el estado viejo.
        using var actDb = TonerTestDb.CreateContext(dbName);
        var result = await BuildService(actDb).LoginAsync(new LoginRequest { Cedula = "1616161616", Password = "Password123!" });

        Assert.True(result.Succeeded);
    }

    private async Task<double> MeasureAverageMillisecondsAsync(string dbName, string cedula, string password, int iterations)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            using var db = TonerTestDb.CreateContext(dbName);
            var service = BuildService(db);
            await service.LoginAsync(new LoginRequest { Cedula = cedula, Password = password });
        }
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds / iterations;
    }

    // Envuelve el hasher real (para que Verify siga siendo criptográficamente representativo en
    // tiempo) pero registra contra qué hash se comparó cada vez — así se puede confirmar que una
    // cuenta bloqueada nunca se compara contra su PasswordHash real.
    private sealed class RecordingPasswordHasher : IPasswordHasher
    {
        public List<string> VerifiedAgainstHashes { get; } = new();

        public string Hash(string password) => RealHasher.Hash(password);

        public bool Verify(string password, string passwordHash)
        {
            VerifiedAgainstHashes.Add(passwordHash);
            return RealHasher.Verify(password, passwordHash);
        }
    }
}
