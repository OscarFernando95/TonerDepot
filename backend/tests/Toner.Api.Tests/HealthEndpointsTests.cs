using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Toner.Api.Health;

namespace Toner.Api.Tests;

// AUDIT.md H-15: /health/live no depende de nada (es lo que sondea App Service); /health/ready sí.
public class HealthEndpointsTests
{
    private static async Task<HttpClient> CreateClientAsync(Action<IHealthChecksBuilder> configure)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    // Mismo FallbackPolicy que Program.cs: sin AllowAnonymous, los endpoints darían 401.
                    services.AddAuthentication("test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DenyAllHandler>("test", _ => { });
                    services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser().Build());
                    configure(services.AddHealthChecks());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e => e.MapTonerHealthChecks());
                });
            })
            .StartAsync();
        return host.GetTestClient();
    }

    private sealed class DenyAllHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
    {
        public DenyAllHandler(
            Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
    }

    private static readonly string[] Ready = { HealthEndpoints.ReadyTag };

    [Fact]
    public async Task Live_Responde200_AunqueLasDependenciasEstenCaidas()
    {
        using var client = await CreateClientAsync(b => b
            .AddCheck("blob", () => HealthCheckResult.Unhealthy("caído"), tags: Ready)
            .AddCheck("postgres", () => HealthCheckResult.Unhealthy("caído"), tags: Ready));

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task Ready_Responde503_SiUnaDependenciaCriticaFalla(string path)
    {
        using var client = await CreateClientAsync(b => b
            .AddCheck("postgres", () => HealthCheckResult.Healthy(), tags: Ready)
            .AddCheck("blob", () => HealthCheckResult.Unhealthy("caído"), tags: Ready));

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Ready_SmtpDegradado_Responde200_ConEstadoDegraded()
    {
        using var client = await CreateClientAsync(b => b
            .AddCheck("postgres", () => HealthCheckResult.Healthy(), tags: Ready)
            .AddCheck("smtp", () => HealthCheckResult.Degraded("sin correo"), tags: Ready));

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Degraded", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_NoFiltraElMensajeDeLaExcepcion()
    {
        using var client = await CreateClientAsync(b => b.AddCheck(
            "blob",
            () => HealthCheckResult.Unhealthy("No se pudo contactar a Blob Storage.", new InvalidOperationException("AccountKey=SECRETO;host=interno")),
            tags: Ready));

        var body = await (await client.GetAsync("/health/ready")).Content.ReadAsStringAsync();

        Assert.Contains("blob", body);
        Assert.DoesNotContain("SECRETO", body);
        Assert.DoesNotContain("interno", body);
    }
}
