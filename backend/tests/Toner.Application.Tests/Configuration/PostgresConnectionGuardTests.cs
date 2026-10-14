using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Configuration;

// AUDIT.md H-43: el contexto RLS se fija con set_config(..., false) — ámbito de SESIÓN — al abrir cada conexión
// física. El PgBouncer integrado de Azure Flexible Server (puerto 6432, modo transacción) reparte las conexiones de
// servidor entre clientes distintos: verificado localmente que un cliente nuevo hereda el rol toner_app_staff y el
// client_id de otro. La app no debe poder arrancar apuntando ahí.
public class PostgresConnectionGuardTests
{
    [Theory]
    [InlineData("Host=toner.postgres.database.azure.com;Port=6432;Database=toner;Username=toner_app;Password=x;Ssl Mode=Require")]
    [InlineData("Host=localhost;Port=6432;Database=toner;Username=toner_app;Password=x")]
    public void PuertoDelPgBouncerDeAzure_NoArranca(string connectionString)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionGuard.EnsureNoTransactionPooler(connectionString, "DefaultConnection"));

        Assert.Contains("6432", ex.Message);
        Assert.Contains("5432", ex.Message);
        Assert.Contains("PgBouncer", ex.Message);
        Assert.Contains("RLS", ex.Message);
        Assert.Contains("DefaultConnection", ex.Message);
        Assert.DoesNotContain("Password", ex.Message); // nunca se repite la cadena
    }

    [Theory]
    [InlineData("Host=toner.postgres.database.azure.com;Port=5432;Database=toner;Username=toner_app;Password=x;Ssl Mode=Require")]
    [InlineData("Host=localhost;Port=5433;Database=toner;Username=toner_app;Password=x")] // docker-compose de dev
    [InlineData("Host=localhost;Database=toner;Username=toner_app;Password=x")]          // sin puerto = 5432
    [InlineData(null)]
    [InlineData("")]
    public void ConexionDirecta_OSinCadena_NoLanza(string? connectionString)
    {
        Assert.Null(Record.Exception(() => PostgresConnectionGuard.EnsureNoTransactionPooler(connectionString, "DefaultConnection")));
    }
}
