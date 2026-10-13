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
        })), NullLogger<AuthService>.Instance, TestSessions.Create(db));

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
    public async Task LoginAsync_CedulaInexistente_VerificaContraUnSenuelo_ConElMismoCostoQueUnHashReal()
    {
        // Antes este test cronometraba BCrypt con reloj de pared y fallaba de forma intermitente
        // cuando otras clases competían por CPU en paralelo (AUDIT.md H-06). La propiedad que de
        // verdad impide el oráculo de tiempos es estructural, y se comprueba sin medir tiempo:
        //   1) "cédula inexistente" hace un Verify() igual que "contraseña incorrecta"
        //      (ver los tests anteriores), y
        //   2) el hash señuelo contra el que compara tiene el MISMO work factor que los hashes
        //      reales: mismo Verify() sobre el mismo costo => mismo tiempo.
        var hasherSpy = new PasswordHasherSpy();
        using var db = TonerTestDb.CreateContext(Guid.NewGuid().ToString());

        await BuildService(db, hasherSpy).LoginAsync(new LoginRequest { Cedula = "0000000000", Password = "cualquiera" });

        var decoyHash = Assert.Single(hasherSpy.VerifiedAgainstHashes);
        var realHash = new BCryptPasswordHasher().Hash("Password123!");
        Assert.Equal(WorkFactorOf(realHash), WorkFactorOf(decoyHash));
    }

    [Fact]
    public async Task LoginAsync_ContrasenaIncorrecta_VerificaContraElHashRealDelUsuario()
    {
        // Contrapartida del test del señuelo: sin esto, un cambio que comparara SIEMPRE contra el
        // señuelo pasaría el test anterior y rompería el login legítimo.
        var hasher = new BCryptPasswordHasher();
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "5050505050");
        user.PasswordHash = hasher.Hash("Password123!");
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        var hasherSpy = new PasswordHasherSpy();
        using var actDb = TonerTestDb.CreateContext(dbName);

        var result = await BuildService(actDb, hasherSpy)
            .LoginAsync(new LoginRequest { Cedula = "5050505050", Password = "contraseña-incorrecta" });

        Assert.False(result.Succeeded);
        Assert.Equal(user.PasswordHash, Assert.Single(hasherSpy.VerifiedAgainstHashes));
    }

    // "$2a$12$<salt+hash>" => 12
    private static int WorkFactorOf(string bcryptHash) => int.Parse(bcryptHash.Split('$')[2]);

    private sealed class PasswordHasherSpy : IPasswordHasher
    {
        public int VerifyCallCount => VerifiedAgainstHashes.Count;

        public List<string> VerifiedAgainstHashes { get; } = new();

        public string Hash(string password) => throw new NotSupportedException();

        public bool Verify(string password, string passwordHash)
        {
            VerifiedAgainstHashes.Add(passwordHash);
            return false;
        }
    }
}
