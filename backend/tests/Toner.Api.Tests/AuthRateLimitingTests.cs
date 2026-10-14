using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Toner.Api.RateLimiting;

namespace Toner.Api.Tests;

// Verifica el hallazgo #2 de SECURITY_AUDIT.md: /api/auth/login ya no acepta intentos ilimitados.
//
// No usa WebApplicationFactory<Program> contra el host real de Toner.Api porque ese host, al
// arrancar, necesita una conexión viva a Postgres (DataSeeder.SeedAsync() y el
// RecurringJob.AddOrUpdate de Hangfire se ejecutan antes de app.Run()) — no disponible en este
// entorno de test. En su lugar se monta un host aislado que llama a la MISMA configuración que Program.cs
// (RateLimitingSetup.AddTonerRateLimiting / UseTonerRateLimiting, AUDIT.md H-13): ya no hay una copia a mano
// que pueda desviarse. Sin cédula en el cuerpo, el límite del login cae a solo IP, que es lo que prueba aquí.
public class AuthRateLimitingTests
{
    private static TestServer CreateTestServer()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    // La MISMA configuración que Program.cs (RateLimitingSetup), con sus valores por defecto.
                    services.AddTonerRateLimiting(new ConfigurationBuilder().Build());
                });
                webBuilder.Configure(app =>
                {
                    // Shim solo-de-test: permite simular distintas IPs de origen leyendo un header,
                    // ya que TestServer no expone una forma directa de variar RemoteIpAddress por request.
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Headers.TryGetValue("X-Test-IP", out var ip))
                        {
                            context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                        }
                        await next();
                    });

                    app.UseRouting();
                    app.UseTonerRateLimiting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/api/auth/login", () => Results.Ok(new { succeeded = false }))
                            .RequireRateLimiting(RateLimitingSetup.LoginPolicy);
                    });
                });
            });

        return hostBuilder.Start().GetTestServer();
    }

    [Fact]
    public async Task Login_SextoIntentoDesdeLaMismaIp_DevuelveTooManyRequestsConRetryAfter()
    {
        using var server = CreateTestServer();
        using var client = server.CreateClient();
        const string ip = "203.0.113.10";

        for (var i = 0; i < 5; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
            request.Headers.Add("X-Test-IP", ip);
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var sixthRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
        sixthRequest.Headers.Add("X-Test-IP", ip);
        var sixthResponse = await client.SendAsync(sixthRequest);

        Assert.Equal(HttpStatusCode.TooManyRequests, sixthResponse.StatusCode);
        Assert.True(sixthResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues));
        var retryAfterSeconds = int.Parse(retryAfterValues!.Single());
        Assert.InRange(retryAfterSeconds, 1, 60);
        Assert.Equal("application/problem+json", sixthResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_IpsDistintasTienenPresupuestosIndependientes()
    {
        using var server = CreateTestServer();
        using var client = server.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
            request.Headers.Add("X-Test-IP", "198.51.100.1");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Una IP distinta no debería verse afectada por el límite que ya consumió la anterior.
        using var otherIpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
        otherIpRequest.Headers.Add("X-Test-IP", "198.51.100.2");
        var otherIpResponse = await client.SendAsync(otherIpRequest);

        Assert.Equal(HttpStatusCode.OK, otherIpResponse.StatusCode);
    }
}
