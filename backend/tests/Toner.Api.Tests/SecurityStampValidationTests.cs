using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Toner.Api.Auth;
using Toner.Application.Auth;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Infrastructure.Auth;
using Toner.Infrastructure.Persistence;

namespace Toner.Api.Tests;

// Verifica el hallazgo #6 de SECURITY_AUDIT.md: un JWT ya emitido debe dejar de funcionar en cuanto
// el SecurityStamp del usuario cambia en BD (cambio de contraseña, reset por admin, o cambio de
// estado activo/inactivo — probados a nivel unitario en Toner.Application.Tests, que cada una de
// esas operaciones sí regenera el stamp). Aquí se prueba el enforcement real: monta el pipeline de
// autenticación real (AddJwtBearer + SecurityStampValidator) contra una BD InMemory, sin necesidad
// de Postgres.
public class SecurityStampValidationTests
{
    private static readonly JwtSettings Settings = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "test-signing-key-at-least-32-characters-long",
        ExpiryMinutes = 60
    };

    private static async Task<(TestServer Server, User User, string Token)> CreateServerWithUserAsync(string dbName)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddDbContext<TonerDbContext>(o => o.UseInMemoryDatabase(dbName));
                    services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TonerDbContext>());
                    services.AddLogging();
                    services.Configure<UserSessionOptions>(o => { o.IdleTimeoutMinutes = 30; o.TouchIntervalSeconds = 60; });
                    services.AddScoped<ISessionService, SessionService>();

                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidIssuer = Settings.Issuer,
                                ValidateAudience = true,
                                ValidAudience = Settings.Audience,
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.SigningKey)),
                                ValidateLifetime = true,
                                ClockSkew = TimeSpan.FromMinutes(1)
                            };
                            options.Events = new JwtBearerEvents
                            {
                                OnTokenValidated = SecurityStampValidator.ValidateAsync
                            };
                        });
                    services.AddAuthorization();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/protegido", () => Results.Ok(new { ok = true }))
                            .RequireAuthorization();
                    });
                });
            })
            .StartAsync();

        var server = host.GetTestServer();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TonerDbContext>();
            var role = new Role { Name = "Coordinador" };
            var user = new User
            {
                Cedula = "5555555555",
                PasswordHash = "no-usado-en-este-test",
                FullName = "Usuario Test",
                RoleId = role.Id,
                Role = role
            };
            db.AddRange(role, user);
            await db.SaveChangesAsync();
        }

        using var seedScope = host.Services.CreateScope();
        var seededUser = await seedScope.ServiceProvider.GetRequiredService<TonerDbContext>()
            .Users.Include(u => u.Role).SingleAsync();

        var tokenGenerator = new JwtTokenGenerator(Options.Create(Settings));
        var sessionId = Guid.NewGuid();
        var (token, expiresAtUtc) = tokenGenerator.GenerateToken(seededUser, sessionId);

        // Igual que AuthService.LoginAsync: cada token emitido tiene su fila de sesión (jti).
        using (var sessionScope = host.Services.CreateScope())
        {
            var sessionDb = sessionScope.ServiceProvider.GetRequiredService<TonerDbContext>();
            var sessions = sessionScope.ServiceProvider.GetRequiredService<ISessionService>();
            await sessions.StartAsync(seededUser, sessionId, expiresAtUtc, "web", "127.0.0.1", "test");
            await sessionDb.SaveChangesAsync();
        }

        return (server, seededUser, token);
    }

    [Fact]
    public async Task Request_ConSecurityStampVigente_Autoriza()
    {
        var (server, _, token) = await CreateServerWithUserAsync(Guid.NewGuid().ToString());
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/protegido");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_ConSecurityStampDesactualizado_DevuelveUnauthorized()
    {
        var (server, user, token) = await CreateServerWithUserAsync(Guid.NewGuid().ToString());

        // Simula lo que hacen ChangePasswordAsync / ResetPasswordAsync / SetActiveStatusAsync: el
        // stamp cambia en BD después de haberse emitido el token, sin que el cliente lo sepa.
        using (var scope = server.Services.CreateScope())
        {
            var scopedDb = scope.ServiceProvider.GetRequiredService<TonerDbContext>();
            var storedUser = await scopedDb.Users.SingleAsync(u => u.Id == user.Id);
            storedUser.SecurityStamp = Guid.NewGuid();
            await scopedDb.SaveChangesAsync();
        }

        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/protegido");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_DeUsuarioDesactivadoConStampVigente_DevuelveUnauthorized()
    {
        // SECURITY_AUDIT_V2.md hallazgo N4: el stamp del token SIGUE SIENDO EL CORRECTO — solo cambia
        // IsActive. Antes de este fix, este caso pasaba: la cuenta quedaba desactivada pero su sesión
        // viva seguía funcionando hasta 8h, porque el validator solo comparaba el stamp. Que hoy
        // funcione vía SetActiveStatusAsync depende de que ese método regenere el stamp; esto verifica
        // que el corte no dependa de ese acoplamiento.
        var (server, user, token) = await CreateServerWithUserAsync(Guid.NewGuid().ToString());

        using (var scope = server.Services.CreateScope())
        {
            var scopedDb = scope.ServiceProvider.GetRequiredService<TonerDbContext>();
            var storedUser = await scopedDb.Users.SingleAsync(u => u.Id == user.Id);
            storedUser.IsActive = false; // el SecurityStamp queda intacto a propósito
            await scopedDb.SaveChangesAsync();
        }

        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/protegido");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetProtegidoAsync(TestServer server, string token)
    {
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/protegido");
    }

    [Fact]
    public async Task Request_ConSesionRevocada_DevuelveUnauthorized()
    {
        var (server, user, token) = await CreateServerWithUserAsync(Guid.NewGuid().ToString());

        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TonerDbContext>();
            var session = await db.UserSessions.SingleAsync(s => s.UserId == user.Id);
            session.RevokedAt = DateTime.UtcNow;
            session.RevokedReason = "Logout";
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetProtegidoAsync(server, token)).StatusCode);
    }

    [Fact]
    public async Task Request_ConSesionInactivaMasDelLimite_DevuelveUnauthorized_YLaCierra()
    {
        var (server, user, token) = await CreateServerWithUserAsync(Guid.NewGuid().ToString());

        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TonerDbContext>();
            var session = await db.UserSessions.SingleAsync(s => s.UserId == user.Id);
            session.LastSeenAt = DateTime.UtcNow.AddMinutes(-31);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetProtegidoAsync(server, token)).StatusCode);

        using var checkScope = server.Services.CreateScope();
        var stored = await checkScope.ServiceProvider.GetRequiredService<TonerDbContext>().UserSessions.SingleAsync();
        Assert.Equal("Inactividad", stored.RevokedReason);
    }

    [Fact]
    public async Task Request_SinFilaDeSesionParaElJti_DevuelveUnauthorized()
    {
        var (server, user, _) = await CreateServerWithUserAsync(Guid.NewGuid().ToString());

        using var scope = server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TonerDbContext>();
        var storedUser = await db.Users.Include(u => u.Role).SingleAsync(u => u.Id == user.Id);
        var (orphanToken, _) = new JwtTokenGenerator(Options.Create(Settings)).GenerateToken(storedUser, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetProtegidoAsync(server, orphanToken)).StatusCode);
    }
}
