using System.Reflection;
using Toner.Api.Controllers;
using Toner.Api.Middleware;

namespace Toner.Api.Tests;

// AUDIT.md H-14: los endpoints de transferencia larga deben seguir marcados; si un refactor quita el atributo, la
// subida o la descarga de una foto volverían a retener una conexión del pool toda la transferencia. El efecto del
// atributo en el middleware se prueba contra Postgres en OpenDbConnectionPostgresTests.
public class NoRequestScopedDbConnectionEndpointsTests
{
    [Theory]
    [InlineData(typeof(TechnicianSelfServiceController), nameof(TechnicianSelfServiceController.UploadEvidence))]
    [InlineData(typeof(EvidenceController), nameof(EvidenceController.GetContent))]
    public void EndpointsDeFotos_NoRetienenConexionDeRequest(Type controller, string action)
    {
        var method = controller.GetMethod(action, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<NoRequestScopedDbConnectionAttribute>());
    }
}
