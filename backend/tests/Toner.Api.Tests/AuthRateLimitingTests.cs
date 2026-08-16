using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Toner.Api.Tests;

// Verifica el hallazgo #2 de SECURITY_AUDIT.md: /api/auth/login ya no acepta intentos ilimitados.
//
// No usa WebApplicationFactory<Program> contra el host real de Toner.Api porque ese host, al
// arrancar, necesita una conexión viva a Postgres (DataSeeder.SeedAsync() y el
// RecurringJob.AddOrUpdate de Hangfire se ejecutan antes de app.Run()) — no disponible en este
// entorno de test. En su lugar se monta un host aislado con la MISMA configuración de la política
// "auth" que Program.cs (PermitLimit = 5, Window = 1 minuto, QueueLimit = 0, RejectionStatusCode =
// 429, header Retry-After en el rechazo), para probar el mecanismo del limiter en sí. Si alguien
// cambia esos valores en Program.cs sin actualizar este archivo, el test deja de reflejar la config
// real — está documentado a propósito para que sea fácil detectar el desvío en una revisión de código.
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
                    services.AddRateLimiter(options =>
                    {
                        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                        options.OnRejected = async (context, cancellationToken) =>
                        {
                            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                            {
                                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                            }

                            await context.HttpContext.Response.WriteAsJsonAsync(
                                new { title = "Demasiadas solicitudes. Intenta de nuevo más tarde.", status = StatusCodes.Status429TooManyRequests },
                                options: null,
                                contentType: "application/problem+json",
                                cancellationToken: cancellationToken);
                        };

                        // Mismos valores que la política "auth" en Program.cs.
                        options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0
                            }));
                    });
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
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/api/auth/login", () => Results.Ok(new { succeeded = false }))
                            .RequireRateLimiting("auth");
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
