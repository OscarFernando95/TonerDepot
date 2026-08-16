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
        var (token, _) = tokenGenerator.GenerateToken(seededUser);

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
}
