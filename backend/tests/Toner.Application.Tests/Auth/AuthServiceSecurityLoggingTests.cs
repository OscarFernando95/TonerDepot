using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Auth;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Infrastructure.Auth;

namespace Toner.Application.Tests.Auth;

// Verifica el hallazgo #8 de SECURITY_AUDIT.md: logins fallidos, logins exitosos, y cambios de
// contraseña fallidos ahora quedan registrados con logging estructurado — antes no dejaban ningún
// rastro (ver AuthService.LoginAsync/ChangePasswordAsync y ExceptionHandlingMiddlewareTests para el
// caso de ForbiddenException).
public class AuthServiceSecurityLoggingTests
{
    private static readonly BCryptPasswordHasher Hasher = new();

    private static AuthService BuildService(Infrastructure.Persistence.TonerDbContext db, ILogger<AuthService> logger) =>
        new(db, Hasher, new JwtTokenGenerator(Options.Create(new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-at-least-32-characters-long",
            ExpiryMinutes = 60
        })), logger, TestSessions.Create(db));

    [Fact]
    public async Task LoginAsync_CedulaInexistente_RegistraWarningConCedulaIpYReason()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var logger = new CapturingLogger<AuthService>();
        var service = BuildService(db, logger);

        var result = await service.LoginAsync(
            new LoginRequest { Cedula = "0000000000", Password = "cualquiera" },
            ipAddress: "203.0.113.10");

        Assert.False(result.Succeeded);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("0000000000", entry.Properties["Cedula"]);
        Assert.Equal("203.0.113.10", entry.Properties["IpAddress"]);
        Assert.Equal("UsuarioNoExiste", entry.Properties["Reason"]);

        // Nunca debe filtrarse ni la contraseña ni ningún hash en el mensaje formateado.
        Assert.DoesNotContain("cualquiera", entry.Message);
    }

    [Fact]
    public async Task LoginAsync_ContraseñaIncorrecta_RegistraWarningConReasonEspecifico()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "6060606060");
        user.PasswordHash = Hasher.Hash("Password123!");
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var logger = new CapturingLogger<AuthService>();
        var service = BuildService(actDb, logger);

        await service.LoginAsync(new LoginRequest { Cedula = "6060606060", Password = "incorrecta" });

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("ContraseñaIncorrecta", entry.Properties["Reason"]);
        Assert.DoesNotContain("incorrecta", entry.Message);
        Assert.DoesNotContain("Password123!", entry.Message);
    }

    [Fact]
    public async Task LoginAsync_LoginExitoso_NoRegistraNingunWarning()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role, cedula: "7070707070");
        user.PasswordHash = Hasher.Hash("Password123!");
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var logger = new CapturingLogger<AuthService>();
        var service = BuildService(actDb, logger);

        var result = await service.LoginAsync(
            new LoginRequest { Cedula = "7070707070", Password = "Password123!" },
            ipAddress: "198.51.100.7");

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("7070707070", entry.Properties["Cedula"]);
        Assert.Equal("198.51.100.7", entry.Properties["IpAddress"]);
        Assert.DoesNotContain("Password123!", entry.Message);
    }

    [Fact]
    public async Task ChangePasswordAsync_ContraseñaActualIncorrecta_RegistraWarningConUserId()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role);
        user.PasswordHash = Hasher.Hash(PasswordDefaults.DefaultPassword);
        arrangeDb.AddRange(role, user);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var logger = new CapturingLogger<AuthService>();
        var service = BuildService(actDb, logger);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest { CurrentPassword = "no-es-la-actual", NewPassword = "NuevaContraseñaSegura1!" },
            ipAddress: "203.0.113.20"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(user.Id, entry.Properties["UserId"]);
        Assert.Equal("203.0.113.20", entry.Properties["IpAddress"]);
        Assert.DoesNotContain("no-es-la-actual", entry.Message);
        Assert.DoesNotContain("NuevaContraseñaSegura1!", entry.Message);
    }
}
