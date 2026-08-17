using Npgsql;

namespace Toner.Application.Tests.Rls;

// Siembra dos clientes con sus sedes, contratos y un ticket, para poder comprobar que un cliente no
// alcanza los datos del otro. Usa el rol OWNER (que además es superusuario en el docker-compose de
// desarrollo, así que puede sembrar sin toparse con las políticas); los tests después consultan como
// toner_app, que sí está sujeto a RLS.
public sealed class RlsFixture
{
    public static readonly Guid ClientA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ClientB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid CityId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private const string OwnerConnectionString =
        "Host=localhost;Port=5433;Database=toner;Username=toner;Password=toner_dev_password;Ssl Mode=Require";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _seeded;

    public async Task EnsureSeededAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_seeded)
            {
                return;
            }

            await using var connection = new NpgsqlConnection(OwnerConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ""Clients"" (""Id"",""Name"",""IsActive"",""IsContractClient"",""CreatedAt"")
                VALUES (@clientA,'Cliente A RLS',true,true,now()), (@clientB,'Cliente B RLS',true,true,now())
                ON CONFLICT (""Id"") DO NOTHING;

                INSERT INTO ""Cities"" (""Id"",""Name"",""StateOrProvince"",""CreatedAt"")
                VALUES (@cityId,'Ciudad RLS','Departamento RLS',now())
                ON CONFLICT (""Id"") DO NOTHING;

                INSERT INTO ""ClientLocations"" (""Id"",""ClientId"",""CityId"",""Name"",""Address"",""IsActive"",""CreatedAt"")
                VALUES (@locationA,@clientA,@cityId,'Sede A','Calle A',true,now()),
                       (@locationB,@clientB,@cityId,'Sede B','Calle B',true,now())
                ON CONFLICT (""Id"") DO NOTHING;

                INSERT INTO ""Contracts"" (""Id"",""ClientId"",""StartDate"",""Status"",""CreatedAt"")
                VALUES (@contractA,@clientA,now(),'Activo',now()),
                       (@contractB,@clientB,now(),'Activo',now())
                ON CONFLICT (""Id"") DO NOTHING;

                INSERT INTO ""ServiceTickets"" (""Id"",""ClientLocationId"",""ReportedByUserId"",""Description"",""Status"",""Priority"",""CreatedAt"")
                SELECT @ticketA, @locationA, u.""Id"", 'Ticket del cliente A', 'Abierto', 'Media', now()
                FROM ""Users"" u LIMIT 1
                ON CONFLICT (""Id"") DO NOTHING;";

            command.Parameters.AddWithValue("clientA", ClientA);
            command.Parameters.AddWithValue("clientB", ClientB);
            command.Parameters.AddWithValue("cityId", CityId);
            command.Parameters.AddWithValue("locationA", Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
            command.Parameters.AddWithValue("locationB", Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));
            command.Parameters.AddWithValue("contractA", Guid.Parse("cccccccc-0000-0000-0000-000000000001"));
            command.Parameters.AddWithValue("contractB", Guid.Parse("dddddddd-0000-0000-0000-000000000002"));
            command.Parameters.AddWithValue("ticketA", Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"));

            await command.ExecuteNonQueryAsync();
            _seeded = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

[CollectionDefinition(nameof(RlsFixtureCollection))]
public sealed class RlsFixtureCollection : ICollectionFixture<RlsFixture>
{
}
