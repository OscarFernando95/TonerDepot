using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Toner.Application.Push;
using Toner.Infrastructure.Health;
using Toner.Infrastructure.Push;

namespace Toner.Application.Tests.Push;

// El push no puede apagarse en silencio donde importa: en producción la API no arranca sin él, y /health/ready lo
// reporta degradado en cualquier entorno.
public class PushStartupGuardTests
{
    private sealed class StubTransport : IPushTransport
    {
        public StubTransport(bool enabled) => IsEnabled = enabled;
        public bool IsEnabled { get; }
        public Task<IReadOnlyList<PushOutcome>> SendAsync(IReadOnlyList<PushTarget> targets, PushMessage message, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static IOptions<FirebaseSettings> Settings(bool? required) => Options.Create(new FirebaseSettings { Required = required });

    [Fact]
    public void Produccion_SinPush_NoArranca_ConUnMensajeQueDiceQueHacer()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PushStartupGuard.EnsureEnabled(new StubTransport(false), Settings(null), isProduction: true));

        Assert.Contains("Firebase__CredentialsPath", ex.Message);
    }

    [Fact]
    public void Desarrollo_SinPush_ArrancaConElAvisoDelLog() =>
        PushStartupGuard.EnsureEnabled(new StubTransport(false), Settings(null), isProduction: false);

    [Fact]
    public void RequiredExplicito_ManejaElComportamientoEnCualquierEntorno()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PushStartupGuard.EnsureEnabled(new StubTransport(false), Settings(true), isProduction: false));

        PushStartupGuard.EnsureEnabled(new StubTransport(false), Settings(false), isProduction: true);
    }

    [Fact]
    public void ConPushActivo_NuncaFalla() =>
        PushStartupGuard.EnsureEnabled(new StubTransport(true), Settings(true), isProduction: true);

    [Fact]
    public async Task HealthCheck_ReportaDegradadoCuandoElPushEstaApagado_SinFiltrarDetalles()
    {
        var check = new PushHealthCheck(new StubTransport(false));
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("push", check, HealthStatus.Degraded, null) };

        var result = await check.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.DoesNotContain("json", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthCheck_SaludableConElPushActivo()
    {
        var check = new PushHealthCheck(new StubTransport(true));
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("push", check, HealthStatus.Degraded, null) };

        Assert.Equal(HealthStatus.Healthy, (await check.CheckHealthAsync(context)).Status);
    }
}
