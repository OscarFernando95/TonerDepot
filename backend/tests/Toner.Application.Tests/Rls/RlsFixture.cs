using Npgsql;
using Toner.Domain.Common;

namespace Toner.Application.Tests.Rls;

// Siembra dos clientes con sus sedes, contratos, un ticket y activos (uno instalado en cada cliente
// y uno en bodega, sin sede), para poder comprobar que un cliente no alcanza los datos del otro. Usa
// el rol OWNER (que además es superusuario en el docker-compose de desarrollo, así que puede sembrar
// sin toparse con las políticas); los tests después consultan como toner_app, que sí está sujeto a RLS.
public sealed class RlsFixture
{
    public static readonly Guid ClientA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ClientB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Usuarios Cliente reales de A y B: el alcance por sede (ClientScope) se consulta en la base, y un usuario que no existe
    // no ve nada — con un id inventado, "el cliente B no ve lo de A" pasaría por la razón equivocada (usuario fantasma).
    public static readonly Guid ClientUserA = Guid.Parse("c11e0000-0000-0000-0000-00000000000a");
    public static readonly Guid ClientUserB = Guid.Parse("c11e0000-0000-0000-0000-00000000000b");
    public static readonly Guid CityId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static readonly Guid AssetOfClientA = Guid.Parse("a55e7000-0000-0000-0000-000000000001");
    public static readonly Guid AssetOfClientB = Guid.Parse("a55e7000-0000-0000-0000-000000000002");

    // Activo en bodega: CurrentClientLocationId NULL. No pertenece a ningún cliente, así que ningún
    // Cliente debe verlo (ver SECURITY_AUDIT_V2.md hallazgo N1).
    public static readonly Guid AssetWithoutLocation = Guid.Parse("a55e7000-0000-0000-0000-000000000003");

    // Autor del ticket y de la evidencia sembrados. La fixture no puede depender de que ya haya usuarios: en una
    // base recién migrada (la del CI) no hay ninguno, porque los crea DataSeeder al arrancar la API. Inactivo y con
    // un hash que no es BCrypt: no puede iniciar sesión en la BD de desarrollo.
    public static readonly Guid FixtureUserId = Guid.Parse("99999999-0000-0000-0000-000000000001");

    // Los roles son datos de referencia que crea DataSeeder al arrancar la API: una base recién migrada (la del CI)
    // no tiene ninguno, y varios tests los buscan por nombre. Idempotente y seguro en paralelo (IX_Roles_Name es único).
    public static async Task EnsureRolesAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ""Roles"" (""Id"",""Name"",""CreatedAt"")
            SELECT gen_random_uuid(), n, now() FROM unnest(@names) AS n
            ON CONFLICT (""Name"") DO NOTHING;";
        command.Parameters.AddWithValue("names", RoleNames.All.ToArray());
        await command.ExecuteNonQueryAsync();
    }

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

            // El usuario de la fixture necesita un rol.
            await EnsureRolesAsync();

            await using var connection = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
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

                INSERT INTO ""Users"" (""Id"",""Cedula"",""PasswordHash"",""FullName"",""RoleId"",""ClientId"",""IsActive"",""MustChangePassword"",""FailedLoginAttempts"",""CreatedAt"")
                SELECT u.id, 'RLS-CLIENTE-' || u.suffix, 'no-es-un-hash', 'Usuario cliente ' || u.suffix, r.""Id"", u.client, true, false, 0, now()
                FROM ""Roles"" r,
                     (VALUES (@clientUserA, 'A', @clientA), (@clientUserB, 'B', @clientB)) AS u(id, suffix, client)
                WHERE r.""Name"" = 'Cliente'
                ON CONFLICT DO NOTHING;

                INSERT INTO ""Users"" (""Id"",""Cedula"",""Email"",""PasswordHash"",""FullName"",""RoleId"",""IsActive"",""MustChangePassword"",""SecurityStamp"",""FailedLoginAttempts"",""CreatedAt"")
                SELECT @userId, 'RLS-FIXTURE', 'rls-fixture@test.local', 'no-es-un-hash', 'Usuario fixture RLS', r.""Id"", false, false, gen_random_uuid(), 0, now()
                FROM ""Roles"" r WHERE r.""Name"" = 'Coordinador'
                ON CONFLICT DO NOTHING;

                -- ClientId es NOT NULL desde la fase 3a (columna denormalizada que usa la política
                -- RLS): hay que sembrarlo explícitamente, igual que lo hace ServiceTicketService.
                INSERT INTO ""ServiceTickets"" (""Id"",""ClientLocationId"",""ClientId"",""ReportedByUserId"",""Description"",""Status"",""Priority"",""CreatedAt"")
                VALUES (@ticketA, @locationA, @clientA, @userId, 'Ticket del cliente A', 'Abierto', 'Media', now())
                ON CONFLICT (""Id"") DO NOTHING;

                -- Evidencia (foto) del ticket del cliente A: la tabla lleva su propio ClientId y política.
                INSERT INTO ""Evidences"" (""Id"",""ClientId"",""Kind"",""SizeBytes"",""ServiceTicketId"",""FileUrl"",""FileName"",""ContentType"",""UploadedByUserId"",""UploadedAt"",""CreatedAt"")
                VALUES (@evidenceA, @clientA, 'Antes', 10, @ticketA, 'rls/a.jpg', 'a.jpg', 'image/jpeg', @userId, now(), now())
                ON CONFLICT (""Id"") DO NOTHING;

                -- Marca y modelo mínimos para poder colgar activos de ellos.
                INSERT INTO ""AssetBrands"" (""Id"",""Name"",""CreatedAt"")
                VALUES (@brandId,'Marca RLS',now())
                ON CONFLICT (""Id"") DO NOTHING;

                INSERT INTO ""AssetModels"" (""Id"",""AssetBrandId"",""Name"",""GeneralPrintThreshold"",""GeneralMonthsInterval"",""UnitsPrintThreshold"",""UnitsMonthsInterval"",""ConsumablesPrintThreshold"",""CreatedAt"")
                VALUES (@modelId,@brandId,'Modelo RLS',30000,6,30000,6,60000,now())
                ON CONFLICT (""Id"") DO NOTHING;

                -- Tres activos: instalado en A, instalado en B, y uno en bodega (sin sede). El de
                -- bodega existe para verificar que CurrentClientLocationId NULL deniega, en vez de
                -- comportarse de forma rara con la subconsulta de la política.
                INSERT INTO ""Assets"" (""Id"",""AssetModelId"",""SerialNumber"",""Type"",""LifecycleStatus"",""CurrentClientLocationId"",""CreatedAt"")
                VALUES (@assetA,@modelId,'SN-RLS-A','Impresora','Instalado',@locationA,now()),
                       (@assetB,@modelId,'SN-RLS-B','Impresora','Instalado',@locationB,now()),
                       (@assetSinSede,@modelId,'SN-RLS-BODEGA','Impresora','EnBodega',NULL,now())
                ON CONFLICT (""Id"") DO NOTHING;";

            command.Parameters.AddWithValue("userId", FixtureUserId);
            command.Parameters.AddWithValue("clientA", ClientA);
            command.Parameters.AddWithValue("clientB", ClientB);
            command.Parameters.AddWithValue("cityId", CityId);
            command.Parameters.AddWithValue("locationA", Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
            command.Parameters.AddWithValue("locationB", Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));
            command.Parameters.AddWithValue("contractA", Guid.Parse("cccccccc-0000-0000-0000-000000000001"));
            command.Parameters.AddWithValue("contractB", Guid.Parse("dddddddd-0000-0000-0000-000000000002"));
            command.Parameters.AddWithValue("ticketA", Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"));
            command.Parameters.AddWithValue("evidenceA", Guid.Parse("eeeeeeee-0000-0000-0000-0000000000e1"));
            command.Parameters.AddWithValue("brandId", Guid.Parse("ffffffff-0000-0000-0000-000000000001"));
            command.Parameters.AddWithValue("modelId", Guid.Parse("ffffffff-0000-0000-0000-000000000002"));
            command.Parameters.AddWithValue("clientUserA", ClientUserA);
            command.Parameters.AddWithValue("clientUserB", ClientUserB);
            command.Parameters.AddWithValue("assetA", AssetOfClientA);
            command.Parameters.AddWithValue("assetB", AssetOfClientB);
            command.Parameters.AddWithValue("assetSinSede", AssetWithoutLocation);

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
