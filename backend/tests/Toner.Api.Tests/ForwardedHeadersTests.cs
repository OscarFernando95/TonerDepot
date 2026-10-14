using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Toner.Api.Tests;

// Verifica el hallazgo #16 de SECURITY_AUDIT.md: sin KnownProxies/KnownNetworks configurados (el
// default en appsettings.json), un X-Forwarded-For arbitrario no debe poder suplantar la IP real —
// si lo hiciera, cualquiera podría spoofear su IP y saltarse el rate limiting por IP del hallazgo #2.
public class ForwardedHeadersTests
{
    private static readonly IPAddress UntrustedDirectConnectionIp = IPAddress.Parse("198.51.100.50");

    [Fact]
    public async Task XForwardedFor_SinProxiesConfiados_NoSobreescribeLaIpReal()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    // Misma configuración que Program.cs, sin agregar nada a KnownProxies/
                    // KnownNetworks — replica el "ReverseProxy" vacío de appsettings.json. El default
                    // (sin tocar) de ASP.NET Core ya confía únicamente en loopback.
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                        options.ForwardLimit = 1;
                    });
                });
                webBuilder.Configure(app =>
                {
                    // TestServer simula la conexión inmediata como loopback por defecto — que SÍ es
                    // confiable por el default de ASP.NET Core. Para probar de verdad "un origen no
                    // confiable no puede spoofear su IP", hay que simular primero una conexión directa
                    // que NO sea loopback (un atacante real conectando sin pasar por ningún proxy).
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = UntrustedDirectConnectionIp;
                        await next();
                    });

                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/ip", (HttpContext ctx) =>
                            Results.Ok(new { ip = ctx.Connection.RemoteIpAddress?.ToString() }));
                    });
                });
            })
            .StartAsync();

        using var client = host.GetTestServer().CreateClient();

        const string spoofedIp = "203.0.113.99";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ip");
        request.Headers.Add("X-Forwarded-For", spoofedIp);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<IpResponse>();

        // El header se ignora por completo: la IP que se ve es la de la conexión directa (el
        // "atacante"), no la que dice el header ni null.
        Assert.Equal(UntrustedDirectConnectionIp.ToString(), body?.Ip);
    }

    [Fact]
    public async Task XForwardedFor_ConProxyConfiadoConfigurado_SiSobreescribeLaIp()
    {
        // Contraparte del test anterior: confirma que el mecanismo funciona cuando el proxy SÍ está
        // en KnownProxies (no que el header simplemente nunca se procese, cualquiera sea la config).
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                        options.ForwardLimit = 1;
                        options.KnownProxies.Add(UntrustedDirectConnectionIp); // acá SÍ es confiable
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = UntrustedDirectConnectionIp;
                        await next();
                    });

                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/ip", (HttpContext ctx) =>
                            Results.Ok(new { ip = ctx.Connection.RemoteIpAddress?.ToString() }));
                    });
                });
            })
            .StartAsync();

        using var client = host.GetTestServer().CreateClient();

        const string realClientIp = "203.0.113.99";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ip");
        request.Headers.Add("X-Forwarded-For", realClientIp);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<IpResponse>();

        Assert.Equal(realClientIp, body?.Ip);
    }

    private sealed record IpResponse(string? Ip);

    // ── AUDIT.md H-13: Azure App Service (ReverseProxy:TrustAllProxies) ─────────────────────────────────────

    private static async Task<string?> SeenIpAsync(Dictionary<string, string?> config, string directIp, string xForwardedFor)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(config).Build();
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    // La MISMA configuración que Program.cs.
                    services.Configure<ForwardedHeadersOptions>(o => Toner.Api.RateLimiting.ForwardedHeadersSetup.Configure(o, configuration));
                });
                webBuilder.Configure(app =>
                {
                    // El front de App Service conecta desde una IP interna que no es loopback ni fija.
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(directIp);
                        await next();
                    });
                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapGet("/ip", (HttpContext ctx) => Results.Ok(new { ip = ctx.Connection.RemoteIpAddress?.ToString() })));
                });
            })
            .StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/ip");
        request.Headers.Add("X-Forwarded-For", xForwardedFor);
        var response = await host.GetTestServer().CreateClient().SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<IpResponse>())?.Ip;
    }

    [Fact]
    public async Task AppService_TrustAllProxies_UsaLaIpQueAnadeElFront_NoLaQueInventaElCliente()
    {
        // El cliente manda su propio X-Forwarded-For falso; el front de App Service añade la IP real AL FINAL.
        var ip = await SeenIpAsync(
            new() { ["ReverseProxy:TrustAllProxies"] = "true" },
            directIp: "10.0.0.5",
            xForwardedFor: "203.0.113.99, 198.51.100.20");

        Assert.Equal("198.51.100.20", ip);
    }

    [Fact]
    public async Task SinTrustAllProxies_ElFrontNoConfiable_NoPuedeCambiarLaIp()
    {
        var ip = await SeenIpAsync(new(), directIp: "10.0.0.5", xForwardedFor: "198.51.100.20");

        Assert.Equal("10.0.0.5", ip);
    }

    [Fact]
    public async Task ConFrontDoorDelante_ForwardLimit2_TomaElSaltoCorrecto()
    {
        // Cliente -> Front Door (añade la IP real) -> front de App Service (añade la IP de Front Door) -> app.
        var ip = await SeenIpAsync(
            new() { ["ReverseProxy:TrustAllProxies"] = "true", ["ReverseProxy:ForwardLimit"] = "2" },
            directIp: "10.0.0.5",
            xForwardedFor: "203.0.113.99, 198.51.100.20, 147.243.1.1");

        Assert.Equal("198.51.100.20", ip);
    }

    // ── AUDIT.md H-07: en .NET 10 KnownNetworks pasa a KnownIPNetworks (System.Net.IPNetwork) ──────────────

    [Fact]
    public async Task KnownNetworks_EnCidr_ConfiaEnElProxyDeEsaRed_YNoEnOtra()
    {
        var config = new Dictionary<string, string?> { ["ReverseProxy:KnownNetworks:0"] = "10.20.0.0/16" };

        Assert.Equal("198.51.100.20", await SeenIpAsync(config, directIp: "10.20.3.4", xForwardedFor: "198.51.100.20"));
        Assert.Equal("10.21.3.4", await SeenIpAsync(config, directIp: "10.21.3.4", xForwardedFor: "198.51.100.20"));
    }
}
