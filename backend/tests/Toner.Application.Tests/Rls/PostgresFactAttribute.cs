using Npgsql;

namespace Toner.Application.Tests.Rls;

// [PostgresFact] marca un test que necesita el Postgres real de docker-compose (con el rol toner_app
// y las políticas RLS ya aplicadas por las migraciones). Si la base no está disponible, el test se
// SALTA con un motivo explícito en vez de fallar — así `dotnet test` sigue siendo verde en una
// máquina sin Docker, pero nadie confunde "saltado" con "pasó".
public sealed class PostgresFactAttribute : FactAttribute
{
    // Rol de la aplicación: sujeto a RLS. Es con el que se prueban las políticas.
    public const string ConnectionString =
        "Host=localhost;Port=5433;Database=toner;Username=toner_app;Password=toner_app_dev_password;Ssl Mode=Require";

    // Rol owner: en el docker-compose de desarrollo es superusuario, así que bypasea RLS y puede
    // sembrar datos de prueba sin toparse con las políticas. Vive acá, junto a la cadena del rol de
    // aplicación, para no repetirla en cada fixture o test que necesite sembrar.
    //
    // Las credenciales son las de desarrollo del docker-compose, ya públicas en docker-compose.yml y
    // .env.example — no filtran nada que no esté ya en el repo. Si algún día se leen del entorno, este
    // es el único sitio a cambiar.
    public const string OwnerConnectionString =
        "Host=localhost;Port=5433;Database=toner;Username=toner;Password=toner_dev_password;Ssl Mode=Require";

    private static readonly Lazy<string?> SkipReason = new(() =>
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            using var roleCommand = connection.CreateCommand();
            roleCommand.CommandText = "SELECT count(*) FROM pg_roles WHERE rolname = 'toner_app_staff'";
            if (Convert.ToInt32(roleCommand.ExecuteScalar()) != 1)
            {
                return "Postgres responde pero falta el rol toner_app_staff, del que dependen las " +
                       "políticas de staff. Corre docker/postgres/create-app-role.sh.";
            }

            using var command = connection.CreateCommand();
            // Las 4 de cliente (sargables, TO toner_app) + las 4 de staff (USING true, TO
            // toner_app_staff) — ver la migración SplitRlsPoliciesByRole.
            command.CommandText =
                "SELECT count(*) FROM pg_policies WHERE schemaname = 'public' " +
                "AND policyname IN ('client_locations_client_isolation', 'contracts_client_isolation', " +
                "'service_tickets_client_isolation', 'assets_client_isolation', " +
                "'client_locations_staff_access', 'contracts_staff_access', " +
                "'service_tickets_staff_access', 'assets_staff_access')";

            var policies = Convert.ToInt32(command.ExecuteScalar());
            return policies == 8
                ? null
                : $"Postgres responde pero faltan políticas RLS (encontradas {policies}/8). " +
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
