using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// TechnicianAssets (vínculo técnico-activo) no tiene ClientId propio: su política consulta Assets."ClientId" (ver
// la migración AddTechnicianAssetsRowLevelSecurity). Estos tests hablan SQL directo como los roles de la app, por
// debajo de los filtros de C#.
[Collection(nameof(RlsFixtureCollection))]
public class TechnicianAssetsRlsTests
{
    private readonly RlsFixture _fixture;

    public TechnicianAssetsRlsTests(RlsFixture fixture) => _fixture = fixture;

    private static readonly Guid LocationB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static NpgsqlConnection NewOwner() => new(PostgresFactAttribute.OwnerConnectionString);

    private static async Task ExecAsync(NpgsqlConnection c, string sql, params (string, object)[] args)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountLinksAsync(NpgsqlConnection c, Guid technicianId)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT count(*) FROM ""TechnicianAssets"" WHERE ""TechnicianId"" = @t";
        cmd.Parameters.AddWithValue("t", technicianId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    // Misma sesión que arma TenantContextInterceptor: staff = SET ROLE toner_app_staff; cliente = rol de la app +
    // app.current_client_id.
    private static async Task<NpgsqlConnection> OpenAsAsync(bool isStaff, Guid? clientId)
    {
        var c = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await c.OpenAsync();
        await ExecAsync(c, "SELECT set_config('role', @role, false), set_config('app.current_client_id', @c, false)",
            ("role", isStaff ? "toner_app_staff" : "none"), ("c", clientId?.ToString() ?? string.Empty));
        return c;
    }

    // Un técnico de prueba (usuario + perfil) con un vínculo a cada uno de los tres activos del fixture.
    private sealed record Seeded(Guid TechnicianId, Guid UserId);

    private static async Task<Seeded> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options;
        await using var db = new TonerDbContext(options);
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Tecnico);
        if (role is null)
        {
            role = TestEntities.Role(RoleNames.Tecnico);
            db.Add(role);
        }

        var user = TestEntities.User(role);
        var technician = TestEntities.Technician(user);
        db.AddRange(user, technician);
        foreach (var assetId in new[] { RlsFixture.AssetOfClientA, RlsFixture.AssetOfClientB, RlsFixture.AssetWithoutLocation })
        {
            db.Add(new TechnicianAsset { TechnicianId = technician.Id, AssetId = assetId });
        }

        await db.SaveChangesAsync();
        return new Seeded(technician.Id, user.Id);
    }

    private static async Task CleanupAsync(Seeded s)
    {
        await using var owner = NewOwner();
        await owner.OpenAsync();
        // Por si un test movió el activo de cliente: se deja donde lo sembró el fixture.
        await ExecAsync(owner, @"UPDATE ""Assets"" SET ""CurrentClientLocationId"" = @loc WHERE ""Id"" = @id",
            ("loc", LocationA), ("id", RlsFixture.AssetOfClientA));
        await ExecAsync(owner, @"DELETE FROM ""TechnicianAssets"" WHERE ""TechnicianId"" = @t", ("t", s.TechnicianId));
        await ExecAsync(owner, @"DELETE FROM ""Technicians"" WHERE ""Id"" = @t", ("t", s.TechnicianId));
        await ExecAsync(owner, @"DELETE FROM ""Users"" WHERE ""Id"" = @u", ("u", s.UserId));
    }

    [PostgresFact]
    public async Task Cliente_SoloVeLosVinculosDeActivosQueSonSuyos()
    {
        await _fixture.EnsureSeededAsync();
        var s = await SeedAsync();
        try
        {
            await using var asA = await OpenAsAsync(isStaff: false, RlsFixture.ClientA);
            Assert.Equal(1, await CountLinksAsync(asA, s.TechnicianId));

            await using var asB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
            Assert.Equal(1, await CountLinksAsync(asB, s.TechnicianId));
        }
        finally
        {
            await CleanupAsync(s);
        }
    }

    [PostgresFact]
    public async Task Cliente_NoVeElVinculoDeUnActivoEnBodega_YStaffVeTodos()
    {
        await _fixture.EnsureSeededAsync();
        var s = await SeedAsync();
        try
        {
            // El vínculo del activo en bodega (ClientId NULL) no lo ve ningún cliente: A y B suman 1 cada uno,
            // no 2 — ya cubierto arriba — y un cliente inexistente ve 0.
            await using var asNobody = await OpenAsAsync(isStaff: false, Guid.NewGuid());
            Assert.Equal(0, await CountLinksAsync(asNobody, s.TechnicianId));

            await using var asStaff = await OpenAsAsync(isStaff: true, null);
            Assert.Equal(3, await CountLinksAsync(asStaff, s.TechnicianId));
        }
        finally
        {
            await CleanupAsync(s);
        }
    }

    [PostgresFact]
    public async Task Cliente_NoPuedeCrearUnVinculoSobreElActivoDeOtroCliente()
    {
        await _fixture.EnsureSeededAsync();
        var s = await SeedAsync();
        try
        {
            await using var asA = await OpenAsAsync(isStaff: false, RlsFixture.ClientA);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(asA,
                @"INSERT INTO ""TechnicianAssets"" (""Id"",""TechnicianId"",""AssetId"",""CreatedAt"") VALUES (@id,@t,@a,now())",
                ("id", Guid.NewGuid()), ("t", s.TechnicianId), ("a", RlsFixture.AssetOfClientB)));
            Assert.Equal("42501", ex.SqlState);
        }
        finally
        {
            await CleanupAsync(s);
        }
    }

    [PostgresFact]
    public async Task SiElActivoCambiaDeCliente_ElVinculoLoSigue_SinCopiaVieja()
    {
        await _fixture.EnsureSeededAsync();
        var s = await SeedAsync();
        try
        {
            await using (var owner = NewOwner())
            {
                await owner.OpenAsync();
                await ExecAsync(owner, @"UPDATE ""Assets"" SET ""CurrentClientLocationId"" = @loc WHERE ""Id"" = @id",
                    ("loc", LocationB), ("id", RlsFixture.AssetOfClientA));
            }

            await using var asA = await OpenAsAsync(isStaff: false, RlsFixture.ClientA);
            Assert.Equal(0, await CountLinksAsync(asA, s.TechnicianId));

            await using var asB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
            Assert.Equal(2, await CountLinksAsync(asB, s.TechnicianId));
        }
        finally
        {
            await CleanupAsync(s);
        }
    }
}
