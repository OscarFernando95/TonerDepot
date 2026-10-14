using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Toner.Infrastructure.Email;
using Toner.Infrastructure.Health;
using Toner.Infrastructure.Storage;

namespace Toner.Application.Tests.Health;

// AUDIT.md H-15. Sin Azurite ni SMTP reales: el éxito de SMTP se prueba contra un servidor falso en un
// puerto efímero; la caída de ambos, contra un puerto cerrado.
public class ExternalHealthChecksTests
{
    private static HealthCheckContext ContextFor(IHealthCheck check, HealthStatus failureStatus) => new()
    {
        Registration = new HealthCheckRegistration("x", check, failureStatus, null)
    };

    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task Smtp_PuertoCerrado_DevuelveElEstadoDeFalloDelRegistro()
    {
        var check = new SmtpHealthCheck(Options.Create(new SmtpSettings { Host = "127.0.0.1", Port = ClosedPort() }));

        var result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.DoesNotContain("127.0.0.1", result.Description);
    }

    // Servidor SMTP falso mínimo, SIN soporte de STARTTLS (como smtp4dev).
    private static (int Port, Task Server) StartFakeSmtpWithoutTls(TcpListener listener)
    {
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var tcp = await listener.AcceptTcpClientAsync();
            using var stream = tcp.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 fake ESMTP");
            while (await reader.ReadLineAsync() is { } line)
            {
                switch (line.Split(' ')[0].ToUpperInvariant())
                {
                    case "EHLO": await writer.WriteLineAsync("250 fake"); break; // no anuncia STARTTLS
                    case "QUIT": await writer.WriteLineAsync("221 bye"); return;
                    default: await writer.WriteLineAsync("250 OK"); break;
                }
            }
        });
        return (port, server);
    }

    [Fact]
    public async Task Smtp_ServidorSinTls_ConModoNone_EsHealthy()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        var (port, server) = StartFakeSmtpWithoutTls(listener);
        var check = new SmtpHealthCheck(Options.Create(new SmtpSettings { Host = "127.0.0.1", Port = port, SecureSocketOptions = "None" }));

        var result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        await Task.WhenAny(server, Task.Delay(2000));
    }

    // AUDIT.md H-04: con el modo por defecto (StartTls), un servidor que no ofrece STARTTLS es un FALLO,
    // no un "sigo en texto plano". Es la misma situación que produce un ataque de STARTTLS stripping.
    [Fact]
    public async Task Smtp_ServidorSinTls_ConModoPorDefecto_FallaEnVezDeDegradarATextoPlano()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        var (port, server) = StartFakeSmtpWithoutTls(listener);
        var check = new SmtpHealthCheck(Options.Create(new SmtpSettings { Host = "127.0.0.1", Port = port }));

        var result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        await Task.WhenAny(server, Task.Delay(2000));
    }

    [Fact]
    public async Task Blob_CuentaInalcanzable_DevuelveElEstadoDeFalloDelRegistro()
    {
        var port = ClosedPort();
        var settings = new EvidenceStorageSettings
        {
            ContainerName = "evidence",
            ConnectionString =
                "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
                "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
                $"BlobEndpoint=http://127.0.0.1:{port}/devstoreaccount1;"
        };
        var check = new BlobStorageHealthCheck(Options.Create(settings));

        var result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Unhealthy));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Cache_NoVuelveAConsultarDentroDelTtl_YSiTrasExpirar()
    {
        var time = new ManualTime();
        var check = new CountingCheck(TimeSpan.FromSeconds(10), time);
        var context = ContextFor(check, HealthStatus.Unhealthy);

        await check.CheckHealthAsync(context);
        await check.CheckHealthAsync(context);
        Assert.Equal(1, check.Calls);

        time.Advance(TimeSpan.FromSeconds(11));
        await check.CheckHealthAsync(context);
        Assert.Equal(2, check.Calls);
    }

    private sealed class CountingCheck : CachedHealthCheck
    {
        public int Calls { get; private set; }
        public CountingCheck(TimeSpan ttl, TimeProvider time) : base(ttl, time) { }

        protected override Task<HealthCheckResult> CheckCoreAsync(HealthCheckContext context, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
