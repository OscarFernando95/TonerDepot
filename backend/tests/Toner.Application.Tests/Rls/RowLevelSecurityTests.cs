using Npgsql;

namespace Toner.Application.Tests.Rls;

// Verifica el hallazgo #5 de SECURITY_AUDIT.md contra el Postgres real: que RLS sea una capa de
// verdad y no decorativa. Estos tests van deliberadamente POR DEBAJO de la capa de aplicación —
// hablan SQL directo como el rol toner_app, ignorando por completo los filtros de C#. Si RLS no
// estuviera aplicando, verían datos de otros clientes.
//
// El filtrado en C# (RequestingUser + los Where de cada servicio) sigue intacto y sigue siendo la
// primera línea de defensa; RLS es la segunda.
[Collection(nameof(RlsFixtureCollection))]
public class RowLevelSecurityTests
{
    private readonly RlsFixture _fixture;

    public RowLevelSecurityTests(RlsFixture fixture) => _fixture = fixture;

    // Réplica exacta de lo que hace TenantContextInterceptor en producción: el bypass de staff ya no
    // es una variable de sesión dentro del predicado, sino QUÉ ROL está activo (CODE_QUALITY_AUDIT.md
    // #2). set_config('role', ...) equivale a SET ROLE, y 'none' a RESET ROLE.
    private static async Task<NpgsqlConnection> OpenAsAsync(bool isStaff, Guid? clientId)
    {
        var connection = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT set_config('role', @role, false), " +
            "       set_config('app.current_client_id', @client_id, false)";
        command.Parameters.AddWithValue("role", isStaff ? "toner_app_staff" : "none");
        command.Parameters.AddWithValue("client_id", clientId?.ToString() ?? string.Empty);
        await command.ExecuteNonQueryAsync();

        return connection;
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $@"SELECT count(*) FROM ""{table}""";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [PostgresFact]
    public async Task Cliente_SoloVeSusPropiasFilas_AunConSqlDirectoQueIgnoraElFiltroDeCSharp()
    {
        await _fixture.EnsureSeededAsync();

        await using var asClientA = await OpenAsAsync(isStaff: false, RlsFixture.ClientA);
        Assert.Equal(1, await CountAsync(asClientA, "ClientLocations"));
        Assert.Equal(1, await CountAsync(asClientA, "Contracts"));
        Assert.Equal(1, await CountAsync(asClientA, "ServiceTickets"));

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        Assert.Equal(1, await CountAsync(asClientB, "ClientLocations"));
        Assert.Equal(1, await CountAsync(asClientB, "Contracts"));
        // El único ticket pertenece al cliente A: B no debe verlo ni con SQL crudo.
        Assert.Equal(0, await CountAsync(asClientB, "ServiceTickets"));
    }

    [PostgresFact]
    public async Task Cliente_SoloSeVeASiMismoEnLaTablaClients()
    {
        // SECURITY_AUDIT_V2.md hallazgo N3: Clients es la tabla RAÍZ de la tenencia (TaxId, correo y
        // teléfono de contacto) y era la única a 0 saltos sin política. Hoy ClientsController es
        // solo-Staff, así que esto es defensa en profundidad: si mañana un endpoint la expusiera a un
        // Cliente, la política ya está.
        await _fixture.EnsureSeededAsync();

        await using var asClientA = await OpenAsAsync(isStaff: false, RlsFixture.ClientA);
        var idsForA = await SelectClientIdsAsync(asClientA);
        Assert.Equal(new[] { RlsFixture.ClientA }, idsForA);

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        var idsForB = await SelectClientIdsAsync(asClientB);
        Assert.Equal(new[] { RlsFixture.ClientB }, idsForB);
    }

    [PostgresFact]
    public async Task Cliente_NoPuedeRenombrarAOtroCliente()
    {
        await _fixture.EnsureSeededAsync();

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        await using var command = asClientB.CreateCommand();
        command.CommandText = @"UPDATE ""Clients"" SET ""Name"" = 'hackeado' WHERE ""Id"" = @otherClient";
        command.Parameters.AddWithValue("otherClient", RlsFixture.ClientA);

        // La política USING no deja ni ver la fila del otro cliente: 0 filas afectadas.
        Assert.Equal(0, await command.ExecuteNonQueryAsync());
    }

    private static async Task<List<Guid>> SelectClientIdsAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT ""Id"" FROM ""Clients"" ORDER BY ""Id""";

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    [PostgresFact]
    public async Task Cliente_SoloVeSusPropiosActivos_YNuncaLosDeBodega()
    {
        // SECURITY_AUDIT_V2.md hallazgo N1: Assets es alcanzable por el rol Cliente
        // (GET /api/assets y /api/assets/{id}) y era la única de las cuatro tablas del portal sin
        // política RLS. Este test va por debajo de C#: habla SQL directo como toner_app.
        await _fixture.EnsureSeededAsync();

        await using var asClientA = await OpenAsAsync(isStaff: false, RlsFixture.ClientA);
        var visibleForA = await SelectAssetIdsAsync(asClientA);
        Assert.Contains(RlsFixture.AssetOfClientA, visibleForA);
        Assert.DoesNotContain(RlsFixture.AssetOfClientB, visibleForA);
        // CurrentClientLocationId NULL: no pertenece a nadie, así que tampoco se ve.
        Assert.DoesNotContain(RlsFixture.AssetWithoutLocation, visibleForA);

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        var visibleForB = await SelectAssetIdsAsync(asClientB);
        Assert.Contains(RlsFixture.AssetOfClientB, visibleForB);
        Assert.DoesNotContain(RlsFixture.AssetOfClientA, visibleForB);
        Assert.DoesNotContain(RlsFixture.AssetWithoutLocation, visibleForB);
    }

    [PostgresFact]
    public async Task Staff_VeLosActivosDeTodosLosClientesYLosDeBodega()
    {
        // Contraparte del anterior: la política no debe romper la operación del back-office, que
        // necesita ver el inventario completo, incluido lo que está en bodega sin cliente asignado.
        await _fixture.EnsureSeededAsync();

        await using var asStaff = await OpenAsAsync(isStaff: true, clientId: null);
        var visible = await SelectAssetIdsAsync(asStaff);

        Assert.Contains(RlsFixture.AssetOfClientA, visible);
        Assert.Contains(RlsFixture.AssetOfClientB, visible);
        Assert.Contains(RlsFixture.AssetWithoutLocation, visible);
    }

    [PostgresFact]
    public async Task Cliente_NoPuedeReasignarseUnActivoDeOtroClienteConUpdate()
    {
        await _fixture.EnsureSeededAsync();

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        await using var command = asClientB.CreateCommand();
        // Intento de robarse el activo del cliente A moviéndolo a la sede propia.
        command.CommandText = @"
            UPDATE ""Assets"" SET ""CurrentClientLocationId"" = @myLocation WHERE ""Id"" = @assetOfA";
        command.Parameters.AddWithValue("myLocation", Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));
        command.Parameters.AddWithValue("assetOfA", RlsFixture.AssetOfClientA);

        // La política USING ni siquiera deja ver la fila: el UPDATE afecta 0 filas.
        Assert.Equal(0, await command.ExecuteNonQueryAsync());
    }

    private static async Task<List<Guid>> SelectAssetIdsAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT ""Id"" FROM ""Assets""";

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    [PostgresFact]
    public async Task Cliente_NoPuedeInsertarFilaAtribuidaAOtroCliente()
    {
        await _fixture.EnsureSeededAsync();

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        await using var command = asClientB.CreateCommand();
        command.CommandText = @"
            INSERT INTO ""ClientLocations"" (""Id"",""ClientId"",""CityId"",""Name"",""Address"",""IsActive"",""CreatedAt"")
            VALUES (gen_random_uuid(), @otherClient, @cityId, 'Sede pirata', 'X', true, now())";
        command.Parameters.AddWithValue("otherClient", RlsFixture.ClientA);
        command.Parameters.AddWithValue("cityId", RlsFixture.CityId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState); // insufficient_privilege / RLS violation
        Assert.Contains("row-level security", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Cliente_NoPuedeRobarFilaDeOtroClienteConUpdate()
    {
        await _fixture.EnsureSeededAsync();

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        await using var command = asClientB.CreateCommand();
        // Intento de apropiarse del contrato del cliente A reasignándolo a sí mismo.
        command.CommandText = @"UPDATE ""Contracts"" SET ""ClientId"" = @me WHERE ""ClientId"" = @other";
        command.Parameters.AddWithValue("me", RlsFixture.ClientB);
        command.Parameters.AddWithValue("other", RlsFixture.ClientA);

        // La política USING ni siquiera deja ver la fila, así que el UPDATE afecta 0 filas.
        Assert.Equal(0, await command.ExecuteNonQueryAsync());
    }

    [PostgresFact]
    public async Task Staff_VeTodosLosClientes()
    {
        await _fixture.EnsureSeededAsync();

        await using var asStaff = await OpenAsAsync(isStaff: true, clientId: null);
        Assert.True(await CountAsync(asStaff, "Clients") >= 2);
        Assert.True(await CountAsync(asStaff, "ClientLocations") >= 2);
        Assert.True(await CountAsync(asStaff, "Contracts") >= 2);
        Assert.True(await CountAsync(asStaff, "ServiceTickets") >= 1);
    }

    [PostgresFact]
    public async Task SinContextoDeSesion_NoSeVeNingunaFila()
    {
        await _fixture.EnsureSeededAsync();

        // Simula el escenario que el TenantContextInterceptor previene lanzando: una conexión donde
        // nadie seteó las variables. RLS deniega todo — fail-closed. (En la app esto nunca llega a
        // pasar porque el interceptor lanza antes; ver TenantContextInterceptorTests.)
        await using var connection = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await connection.OpenAsync();

        foreach (var table in TablesWithRls)
        {
            Assert.Equal(0, await CountAsync(connection, table));
        }
    }

    // Las 12 tablas con política RLS. Se enumeran acá para que los tests de "sin contexto no se ve
    // nada" y "toda tabla protegida tiene sus dos políticas" cubran automáticamente cualquier tabla
    // que se agregue en el futuro sin tener que acordarse de sumarla a cada test.
    private static readonly string[] TablesWithRls =
    {
        "Clients", "ClientLocations", "Contracts", "ServiceTickets", "Assets",
        "MeterReadings", "MaintenanceOrders", "MaintenanceSchedules",
        "AssetStatusLogs", "ContractAssets", "TimeLogs", "AssignmentHistories"
    };

    [PostgresFact]
    public async Task TodaTablaConRls_TienePoliticaDeClienteYDeStaff_ForzadaTambienParaElOwner()
    {
        // Verifica la FORMA de la protección, no solo su efecto: cada tabla debe tener exactamente dos
        // políticas (una por rol) y FORCE ROW LEVEL SECURITY. Detecta una tabla que quede a medias —
        // por ejemplo con RLS habilitado pero sin la política de staff, que dejaría al back-office sin
        // ver nada, o sin FORCE, que dejaría al owner fuera del alcance de las políticas.
        await using var connection = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
        await connection.OpenAsync();

        foreach (var table in TablesWithRls)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.relrowsecurity, c.relforcerowsecurity,
                       (SELECT count(*) FROM pg_policies p
                        WHERE p.schemaname = 'public' AND p.tablename = c.relname)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public' AND c.relname = @table";
            command.Parameters.AddWithValue("table", table);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), $"La tabla {table} no existe.");

            Assert.True(reader.GetBoolean(0), $"{table} no tiene ROW LEVEL SECURITY habilitado.");
            Assert.True(reader.GetBoolean(1), $"{table} no tiene FORCE ROW LEVEL SECURITY.");
            Assert.True(reader.GetInt64(2) == 2, $"{table} tiene {reader.GetInt64(2)} políticas, se esperaban 2 (cliente + staff).");
        }
    }

    [PostgresFact]
    public async Task ToneApp_NoPuedeDesactivarRlsNiHacerDdlSobreTablasDeNegocio()
    {
        // La propiedad de seguridad que justifica todo el fix: aunque un atacante logre ejecutar SQL
        // arbitrario como la aplicación, no puede apagar las políticas ni destruir el esquema.
        await using var connection = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await connection.OpenAsync();

        await using (var disableRls = connection.CreateCommand())
        {
            disableRls.CommandText = @"ALTER TABLE ""Contracts"" DISABLE ROW LEVEL SECURITY";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => disableRls.ExecuteNonQueryAsync());
            Assert.Contains("must be owner", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = @"DROP TABLE ""Clients""";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => drop.ExecuteNonQueryAsync());
            Assert.Contains("must be owner", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var readLogs = connection.CreateCommand())
        {
            // ExceptionLogs guarda stack traces y correos: la app escribe pero no debe poder leer.
            readLogs.CommandText = @"SELECT count(*) FROM ""ExceptionLogs""";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => readLogs.ExecuteScalarAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    // CODE_QUALITY_AUDIT.md #2: app.is_staff era la llave del bypass y dejó de serlo — ahora el
    // bypass es el ROL activo. Este test fija esa propiedad: si alguien reintrodujera un
    // "OR current_setting('app.is_staff') = 'on'" en cualquier política (revirtiendo la mejora de
    // plan sin que ningún otro test lo note), esto se pone rojo.
    [PostgresFact]
    public async Task AppIsStaff_YaNoOtorgaAcceso_ElBypassEsElRol()
    {
        await _fixture.EnsureSeededAsync();

        await using var connection = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await connection.OpenAsync();

        await using (var poison = connection.CreateCommand())
        {
            // Sin SET ROLE y sin cliente, pero con la variable vieja en 'on': antes esto daba acceso
            // total a las cuatro tablas.
            poison.CommandText =
                "SELECT set_config('role', 'none', false), " +
                "       set_config('app.is_staff', 'on', false), " +
                "       set_config('app.current_client_id', '', false)";
            await poison.ExecuteNonQueryAsync();
        }

        foreach (var table in new[] { "ClientLocations", "Contracts", "ServiceTickets", "Assets" })
        {
            Assert.Equal(0, await CountAsync(connection, table));
        }

        // Y el rol sí abre la puerta, para que el test no pueda pasar por un fallo genérico de acceso.
        await using (var asStaff = connection.CreateCommand())
        {
            asStaff.CommandText = "SELECT set_config('role', 'toner_app_staff', false)";
            await asStaff.ExecuteNonQueryAsync();
        }

        Assert.True(await CountAsync(connection, "ClientLocations") > 0);
    }

    [PostgresFact]
    public async Task Evidencias_ClienteSoloVeLasSuyas_ElStaffVeTodas_YNoSePuedeEscribirEnNombreDeOtro()
    {
        // Las fotos de un ticket pertenecen a un cliente: Evidences lleva su propio ClientId y política
        // (antes no tenía ninguna porque no había camino de escritura; ver SECURITY_AUDIT_V2.md N11 / #23).
        await _fixture.EnsureSeededAsync();

        await using var asClientA = await OpenAsAsync(isStaff: false, RlsFixture.ClientA);
        Assert.Equal(1, await CountAsync(asClientA, "Evidences"));

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        Assert.Equal(0, await CountAsync(asClientB, "Evidences"));

        await using var asStaff = await OpenAsAsync(isStaff: true, clientId: null);
        Assert.True(await CountAsync(asStaff, "Evidences") >= 1);

        // WITH CHECK: el cliente B no puede insertar una evidencia que diga ser del cliente A.
        await using var insert = asClientB.CreateCommand();
        insert.CommandText = @"
            INSERT INTO ""Evidences"" (""Id"",""ClientId"",""Kind"",""SizeBytes"",""ServiceTicketId"",""FileUrl"",""FileName"",""ContentType"",""UploadedByUserId"",""UploadedAt"",""CreatedAt"")
            SELECT gen_random_uuid(), @clientA, 'Antes', 1, @ticketA, 'x', 'x', 'image/jpeg', u.""Id"", now(), now() FROM ""Users"" u LIMIT 1";
        insert.Parameters.AddWithValue("clientA", RlsFixture.ClientA);
        insert.Parameters.AddWithValue("ticketA", Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"));
        await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
    }
}
