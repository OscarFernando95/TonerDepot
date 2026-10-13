using Toner.Infrastructure.Jobs;

namespace Toner.Application.Tests.Jobs;

// Zona crítica 5 (AUDIT.md §6): Hangfire guarda tipo, método y argumentos de cada job encolado. Si esta firma
// cambia, los correos de contraseña que estén en cola o reintentando al desplegar dejan de ejecutarse — en
// silencio. Este test convierte ese renombrado en un fallo visible antes de llegar a producción.
public class SendGeneratedPasswordEmailJobContractTests
{
    [Fact]
    public void LaFirmaQueHangfireSerializa_NoCambia()
    {
        var type = typeof(SendGeneratedPasswordEmailJob);
        Assert.Equal("Toner.Infrastructure.Jobs.SendGeneratedPasswordEmailJob", type.FullName);
        Assert.Equal("Toner.Infrastructure", type.Assembly.GetName().Name);

        var method = type.GetMethod("RunAsync", new[] { typeof(string), typeof(string), typeof(CancellationToken) });
        Assert.NotNull(method);
        Assert.Equal(new[] { "cedula", "encryptedPassword", "cancellationToken" }, method!.GetParameters().Select(p => p.Name));
        Assert.Equal(typeof(Task), method.ReturnType);
    }
}
