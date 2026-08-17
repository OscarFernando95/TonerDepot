using Npgsql;

namespace Toner.Application.Tests.Rls;

// [PostgresFact] marca un test que necesita el Postgres real de docker-compose (con el rol toner_app
// y las políticas RLS ya aplicadas por las migraciones). Si la base no está disponible, el test se
// SALTA con un motivo explícito en vez de fallar — así `dotnet test` sigue siendo verde en una
// máquina sin Docker, pero nadie confunde "saltado" con "pasó".
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string ConnectionString =
        "Host=localhost;Port=5433;Database=toner;Username=toner_app;Password=toner_app_dev_password;Ssl Mode=Require";

    private static readonly Lazy<string?> SkipReason = new(() =>
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT count(*) FROM pg_policies WHERE schemaname = 'public' " +
                "AND policyname IN ('client_locations_client_isolation', 'contracts_client_isolation', " +
                "'service_tickets_client_isolation')";

            var policies = Convert.ToInt32(command.ExecuteScalar());
            return policies == 3
                ? null
                : $"Postgres responde pero faltan políticas RLS (encontradas {policies}/3). " +
                  "Corre 'dotnet ef database update' con MigrationsConnection.";
        }
        catch (Exception ex)
        {
            return $"Postgres no disponible en localhost:5433 como toner_app ({ex.GetType().Name}). " +
                   "Levanta docker compose y corre las migraciones para ejecutar este test.";
        }
    });

    public override string? Skip
    {
        get => SkipReason.Value;
        set { /* el motivo lo determina la disponibilidad de la base, no el autor del test */ }
    }
}
