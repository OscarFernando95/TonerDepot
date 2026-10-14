using System.Data;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Api.Middleware;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Common;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-14: OpenDbConnectionMiddleware abría la conexión ANTES del controller y la retenía toda la request —
// en la subida de una foto (hasta 12 MB por red móvil) eso es una conexión del pool ocupada durante toda la
// transferencia. Se monta la cadena REAL de tenencia (TenantContextMiddleware + TenantContextInterceptor +
// OpenDbConnectionMiddleware) contra el Postgres real con el rol de la aplicación.
[Collection(nameof(RlsFixtureCollection))]
public class OpenDbConnectionPostgresTests
{
    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new(ClaimTypes.Role, role.ToString()) };
            if (Request.Headers.TryGetValue("X-Test-Client", out var client))
            {
                claims.Add(new Claim("client_id", client.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), "test")));
        }
    }

    private sealed record Probe(string StateInHandler, string CurrentUser, string? ClientSetting);

    private static async Task<HttpClient> CreateClientAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("test", _ => { });
                    services.AddAuthorization();
                    // Mismos registros que Program.cs para la cadena de tenencia.
                    services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
                    services.AddSingleton<TenantContextInterceptor>();
                    services.AddScoped(sp => new TonerDbContext(new DbContextOptionsBuilder<TonerDbContext>()
                        .UseNpgsql(PostgresFactAttribute.ConnectionString)
                        .AddInterceptors(sp.GetRequiredService<TenantContextInterceptor>())
                        .Options));
                });
                web.Configure(app =>
                {
                    // Mismo orden que Program.cs.
                    app.UseRouting();
                    app.UseMiddleware<TenantContextMiddleware>();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseMiddleware<OpenDbConnectionMiddleware>();
                    app.UseEndpoints(e =>
                    {
                        e.MapGet("/normal", ProbeAsync);
                        e.MapGet("/transfer", ProbeAsync).WithMetadata(new NoRequestScopedDbConnectionAttribute());
                    });
                });
            })
            .StartAsync();
        return host.GetTestClient();
    }

    // Estado de la conexión de la request DENTRO del handler (lo que vería el controller mientras lee el formulario o
    // sube a Blob) y el contexto con que corre una consulta hecha después.
    private static async Task ProbeAsync(HttpContext context)
    {
        var db = context.RequestServices.GetRequiredService<TonerDbContext>();
        var state = db.Database.GetDbConnection().State.ToString();
        var currentUser = await db.Database.SqlQueryRaw<string>("SELECT current_user::text AS \"Value\"").SingleAsync();
        var client = await db.Database.SqlQueryRaw<string>("SELECT current_setting('app.current_client_id', true) AS \"Value\"").SingleAsync();
        await context.Response.WriteAsJsonAsync(new Probe(state, currentUser, client));
    }

    private static async Task<Probe> GetAsync(HttpClient client, string path, string role, Guid? clientId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Role", role);
        if (clientId is not null) request.Headers.Add("X-Test-Client", clientId.ToString());
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<Probe>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    [PostgresFact]
    public async Task EndpointNormal_LaConexionDeLaRequestEstaAbierta()
    {
        using var client = await CreateClientAsync();

        var probe = await GetAsync(client, "/normal", RoleNames.Coordinador);

        Assert.Equal(nameof(ConnectionState.Open), probe.StateInHandler);
    }

    [PostgresFact]
    public async Task EndpointDeTransferencia_NoRetieneConexionDuranteElHandler()
    {
        using var client = await CreateClientAsync();

        var probe = await GetAsync(client, "/transfer", RoleNames.Tecnico);

        Assert.Equal(nameof(ConnectionState.Closed), probe.StateInHandler);
    }

    [PostgresFact]
    public async Task EndpointDeTransferencia_LasConsultasSiguenConElContextoDeTenenciaCorrecto()
    {
        using var client = await CreateClientAsync();

        // Técnico (staff a nivel de BD): SET ROLE toner_app_staff, igual que con la conexión de request.
        var technician = await GetAsync(client, "/transfer", RoleNames.Tecnico);
        Assert.Equal("toner_app_staff", technician.CurrentUser);

        // Cliente: su client_id en la variable de sesión que leen las políticas RLS.
        var clientId = Guid.NewGuid();
        var customer = await GetAsync(client, "/transfer", RoleNames.Cliente, clientId);
        Assert.Equal("toner_app", customer.CurrentUser);
        Assert.Equal(clientId.ToString(), customer.ClientSetting);
    }
}
