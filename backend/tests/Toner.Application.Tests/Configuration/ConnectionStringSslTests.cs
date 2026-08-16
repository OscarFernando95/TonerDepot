using System.Text.Json;
using Npgsql;

namespace Toner.Application.Tests.Configuration;

// Verifica el hallazgo #10 de SECURITY_AUDIT.md: el connection string que documenta
// appsettings.Development.json.example (la plantilla que cualquiera copia para levantar un entorno
// nuevo, ver README.md) exige SSL en vez de heredar el default "Prefer" de Npgsql, que degrada a
// texto plano en silencio si el servidor no lo soporta. No requiere Postgres real ni Docker — solo
// parsea el string documentado, así que detecta typos/regresiones de configuración sin
// infraestructura.
public class ConnectionStringSslTests
{
    [Fact]
    public void AppsettingsDevelopmentExample_ExigeSslModeRequireComoMinimo()
    {
        var examplePath = FindBackendFile("src/Toner.Api/appsettings.Development.json.example");
        using var document = JsonDocument.Parse(File.ReadAllText(examplePath));
        var connectionString = document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.True(
            builder.SslMode is SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull,
            $"SslMode fue '{builder.SslMode}' en {examplePath} — se esperaba Require (o superior), " +
            "no el default 'Prefer', que degrada a texto plano en silencio si el servidor no soporta SSL.");
    }

    private static string FindBackendFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Toner.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("No se encontró backend/Toner.sln subiendo desde el directorio de ejecución del test.");
        }

        return Path.Combine(dir.FullName, relativePath);
    }
}
