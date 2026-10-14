using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Api.RateLimiting;

namespace Toner.Api.Tests;

// AUDIT.md H-13: el límite se repartía por IP, así que una oficina detrás de NAT (o técnicos en CGNAT móvil)
// compartía un único cupo. Se monta EXACTAMENTE la configuración de Program.cs (AddTonerRateLimiting /
// UseTonerRateLimiting) con cupos pequeños por configuración.
public class RateLimitingTests
{
    private const string SharedIp = "203.0.113.10";

    // Autenticación de prueba: el header X-Test-User hace las veces de un JWT válido con ese sub.
    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()) }, "test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "test")));
        }
    }

    private static async Task<HttpClient> CreateClientAsync(int globalLimit = 3, int loginLimit = 5, int passwordLimit = 5)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["RateLimiting:GlobalPermitLimit"] = globalLimit.ToString(),
                        ["RateLimiting:LoginPermitLimit"] = loginLimit.ToString(),
                        ["RateLimiting:PasswordChangePermitLimit"] = passwordLimit.ToString()
                    }).Build();
                    services.AddRouting();
                    services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("test", _ => { });
                    services.AddTonerRateLimiting(configuration);
                });
                web.Configure(app =>
                {
                    // Todos los clientes del test salen por la MISMA IP pública (NAT de una oficina).
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers.TryGetValue("X-Test-IP", out var ip) ? ip.ToString() : SharedIp);
                        await next();
                    });
                    app.UseRouting();
                    // Mismo orden que Program.cs: autenticación ANTES del limitador.
                    app.UseAuthentication();
                    app.UseTonerRateLimiting();
                    app.UseEndpoints(e =>
                    {
                        e.MapGet("/api/data", () => Results.Ok());
                        // Devuelve el cuerpo tal como le llega: prueba que el middleware de la cédula lo rebobina.
                        e.MapPost("/api/auth/login", async ctx =>
                            await ctx.Response.WriteAsync(await new StreamReader(ctx.Request.Body).ReadToEndAsync()))
                            .RequireRateLimiting(RateLimitingSetup.LoginPolicy);
                        e.MapPost("/api/auth/change-password", () => Results.Ok()).RequireRateLimiting(RateLimitingSetup.PasswordChangePolicy);
                    });
                });
            })
            .StartAsync();
        return host.GetTestClient();
    }

    private static HttpRequestMessage Get(string? user, string? ip = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/data");
        if (user is not null) request.Headers.Add("X-Test-User", user);
        if (ip is not null) request.Headers.Add("X-Test-IP", ip);
        return request;
    }

    private static HttpRequestMessage Login(string cedula, string? ip = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { cedula, password = "x" })
        };
        if (ip is not null) request.Headers.Add("X-Test-IP", ip);
        return request;
    }

    [Fact]
    public async Task DosUsuariosConLaMismaIp_NoSeLimitanEntreSi()
    {
        using var client = await CreateClientAsync(globalLimit: 3);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("usuario-a"))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Get("usuario-a"))).StatusCode);

        // Mismo NAT, otro usuario: tiene su propio cupo.
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("usuario-b"))).StatusCode);
    }

    [Fact]
    public async Task Anonimos_SeLimitanPorIp()
    {
        using var client = await CreateClientAsync(globalLimit: 2);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get(null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get(null))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Get(null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get(null, ip: "198.51.100.7"))).StatusCode);
    }

    [Fact]
    public async Task Login_DosCedulasDesdeLaMismaIp_NoSeLimitanEntreSi_LaMismaCedulaSi()
    {
        using var client = await CreateClientAsync(globalLimit: 100, loginLimit: 2);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Login("1001"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Login("1001"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Login("1001"))).StatusCode);

        // Compañero de oficina (misma IP), otra cédula: no está bloqueado.
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Login("2002"))).StatusCode);
        // La misma cédula con espacios es la misma cédula.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Login(" 1001 "))).StatusCode);
    }

    [Fact]
    public async Task Login_ElCuerpoLlegaEnteroAlEndpoint_TrasLeerLaCedula()
    {
        using var client = await CreateClientAsync();

        var response = await client.SendAsync(Login("3003"));
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {raw}");
        Assert.Equal("{\"cedula\":\"3003\",\"password\":\"x\"}", raw);
    }

    [Fact]
    public async Task CambioDeContrasena_SeLimitaPorUsuario()
    {
        using var client = await CreateClientAsync(globalLimit: 100, passwordLimit: 1);

        HttpRequestMessage Change(string user)
        {
            var r = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password");
            r.Headers.Add("X-Test-User", user);
            return r;
        }

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Change("usuario-a"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Change("usuario-a"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Change("usuario-b"))).StatusCode);
    }
}
