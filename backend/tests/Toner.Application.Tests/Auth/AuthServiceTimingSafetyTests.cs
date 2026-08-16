using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toner.Application.Auth;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Interfaces;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Infrastructure.Auth;

namespace Toner.Application.Tests.Auth;

// Verifica el hallazgo #7 de SECURITY_AUDIT.md: antes del fix, "user is null || !user.IsActive ||
// !Verify(...)" hacía cortocircuito y nunca llamaba a IPasswordHasher.Verify() cuando el usuario no
// existía o estaba inactivo. Eso hacía que "cédula inexistente" respondiera en microsegundos y
// "cédula válida, contraseña incorrecta" tardara lo que tarda un BCrypt real (~200-300ms) — una
// diferencia medible por red que permite enumerar cédulas válidas aunque el mensaje de error sea
// siempre el mismo.
public class AuthServiceTimingSafetyTests
{
    private static AuthService BuildService(Infrastructure.Persistence.TonerDbContext db, IPasswordHasher hasher) =>
        new(db, hasher, new JwtTokenGenerator(Options.Create(new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-at-least-32-characters-long",
            ExpiryMinutes = 60
        })), NullLogger<AuthService>.Instance);

    [Fact]
    public async Task LoginAsync_CedulaInexistente_IgualLlamaAVerifyUnaVez()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var hasherSpy = new PasswordHasherSpy();
        var service = BuildService(db, hasherSpy);

        var result = await service.LoginAsync(new LoginRequest { Cedula = "0000000000", Password = "cualquiera" });

        Assert.False(result.Succeeded);
        Assert.Equal(1, hasherSpy.VerifyCallCount);
    }

    [Fact]
    public async Task LoginAsync_UsuarioInactivo_IgualLlamaAVerifyUnaVez()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "4040404040");
        user.PasswordHash = "cualquier-hash";
        user.IsActive = false;
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var hasherSpy = new PasswordHasherSpy();
        var service = BuildService(actDb, hasherSpy);

        var result = await service.LoginAsync(new LoginRequest { Cedula = "4040404040", Password = "cualquiera" });

        Assert.False(result.Succeeded);
        Assert.Equal(1, hasherSpy.VerifyCallCount);
    }

    [Fact]
    public async Task LoginAsync_TiempoDeCedulaInexistenteEsComparableAlDeContrasenaIncorrecta()
    {
        // Sanity check con el hasher real (BCrypt, workFactor 12): confirma que "cédula inexistente"
        // no es órdenes de magnitud más rápido que "cédula válida, password incorrecta" — que es
        // justo la señal que permitiría enumerar cédulas por tiempos de red. Tolerancia amplia (>=
        // 40% del tiempo del camino real) para no volverse un test frágil por ruido de scheduling;
        // lo que importa es descartar el caso "microsegundos vs. cientos de milisegundos" de antes.
        var hasher = new BCryptPasswordHasher();

        var dbNameKnown = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbNameKnown);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "5050505050");
        user.PasswordHash = hasher.Hash("Password123!");
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        const int iterations = 3;

        var knownElapsedMs = await MeasureAverageMillisecondsAsync(dbNameKnown, hasher, "5050505050", iterations);
        var unknownElapsedMs = await MeasureAverageMillisecondsAsync(Guid.NewGuid().ToString(), hasher, "0000000000", iterations);

        Assert.True(
            unknownElapsedMs >= knownElapsedMs * 0.4,
            $"El camino de cédula inexistente ({unknownElapsedMs:F1}ms) fue desproporcionadamente más " +
            $"rápido que el de contraseña incorrecta ({knownElapsedMs:F1}ms) — sugiere que se está " +
            "saltando el hashing.");
    }

    private static async Task<double> MeasureAverageMillisecondsAsync(string dbName, IPasswordHasher hasher, string cedula, int iterations)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            using var db = TonerTestDb.CreateContext(dbName);
            var service = BuildService(db, hasher);
            await service.LoginAsync(new LoginRequest { Cedula = cedula, Password = "contraseña-incorrecta" });
        }
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds / iterations;
    }

    private sealed class PasswordHasherSpy : IPasswordHasher
    {
        public int VerifyCallCount { get; private set; }

        public string Hash(string password) => throw new NotSupportedException();

        public bool Verify(string password, string passwordHash)
        {
            VerifyCallCount++;
            return false;
        }
    }
}
