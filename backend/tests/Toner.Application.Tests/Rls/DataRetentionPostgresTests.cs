using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Evidences;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Infrastructure.Jobs;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-27. Contra el Postgres real y con el rol de la aplicación (toner_app): InMemory no
// soporta ExecuteDelete, y es justamente el rol sin privilegios de owner el que tiene que poder borrar.
[Collection(nameof(RlsFixtureCollection))]
public class DataRetentionPostgresTests
{
    // "Ahora" ficticio en el año 2000, a propósito: el job purga TODO lo anterior al corte en la base, y esta es la BD
    // de desarrollo compartida. Con un ahora real borraría también filas reales de sesiones, logs o evidencias;
    // con el año 2000 solo califican las filas que siembra cada test.
    private static readonly DateTime Now = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(ITenantContextAccessor tenant) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(tenant))
            .Options);

    private sealed class NoopExceptionLogger : IExceptionLogger
    {
        public Task LogAsync(string source, Exception exception, string? requestMethod = null, string? requestPath = null,
            int? statusCode = null, Guid? userId = null, string? userEmail = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingStorage : IEvidenceStorage
    {
        public List<string> Deleted { get; } = new();
        public Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<Stream?>(null);
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { Deleted.Add(key); return Task.CompletedTask; }
    }

    private static DataRetentionJob Job(TonerDbContext db, ITenantContextAccessor tenant, DataRetentionOptions options, IEvidenceStorage? storage = null) =>
        new(db, Options.Create(options), storage ?? new RecordingStorage(), new NoopExceptionLogger(), NullLogger<DataRetentionJob>.Instance, tenant);

    private static UserSession Session(Guid userId, DateTime issuedAt, DateTime expiresAt, DateTime? revokedAt, DateTime lastSeenAt) => new()
    {
        UserId = userId,
        ClientType = "web",
        IssuedAt = issuedAt,
        ExpiresAt = expiresAt,
        LastSeenAt = lastSeenAt,
        RevokedAt = revokedAt,
        RevokedReason = revokedAt is null ? null : "Logout",
        EnforcesSingleSession = false
    };

    [PostgresFact]
    public async Task Purga_SoloSesionesTerminadasHaceMasDeLaRetencion_YNuncaUnaViva()
    {
        var days = Now.AddDays(-100);
        var userId = Guid.Empty;
        try
        {
            await using (var owner = OwnerContext())
            {
                var role = await owner.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Coordinador) ?? TestEntities.Role(RoleNames.Coordinador);
                if (owner.Entry(role).State == EntityState.Detached) owner.Add(role);
                var user = TestEntities.User(role);
                userId = user.Id;
                owner.Add(user);
                owner.UserSessions.AddRange(
                    // 0: revocada hace 100 días -> se purga.
                    Session(userId, days, days.AddDays(1), revokedAt: days.AddDays(1), lastSeenAt: days),
                    // 1: vencida hace 100 días y nunca revocada -> se purga.
                    Session(userId, days, days.AddDays(1), revokedAt: null, lastSeenAt: days.AddHours(2)),
                    // 2: revocada hace 10 días -> se conserva (dentro de la retención).
                    Session(userId, Now.AddDays(-11), Now.AddDays(-9), revokedAt: Now.AddDays(-10), lastSeenAt: Now.AddDays(-10)),
                    // 3: viva, emitida hace 100 días (token largo): sin revocar y con ExpiresAt futuro -> se conserva.
                    Session(userId, days, Now.AddDays(5), revokedAt: null, lastSeenAt: Now.AddMinutes(-5)),
                    // 4: vencida hace 100 días pero vista hace 2 días -> se conserva (LastSeenAt dentro de la retención).
                    Session(userId, days, days.AddDays(1), revokedAt: null, lastSeenAt: Now.AddDays(-2)));
                await owner.SaveChangesAsync();
            }

            var ids = await GetSessionIdsAsync(userId);
            Assert.Equal(5, ids.Count);

            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);
            await using var appDb = AppContext(tenant);
            var job = Job(appDb, tenant, new DataRetentionOptions { UserSessionsDays = 90 });

            var deleted = await job.ApplyAsync(Now);

            Assert.True(deleted >= 2, $"Se esperaban al menos 2 eliminadas, fueron {deleted}.");
            var remaining = await GetSessionIdsAsync(userId);
            Assert.Equal(3, remaining.Count);
            Assert.Equal(
                new[] { "revocada-hace-10d", "vencida-pero-vista-hace-2d", "viva-token-largo" },
                (await GetMarkersAsync(userId)).OrderBy(x => x));
        }
        finally
        {
            if (userId != Guid.Empty)
            {
                await using var cleanup = OwnerContext();
                await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"UserSessions\" WHERE \"UserId\" = {0}", userId);
                await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Id\" = {0}", userId);
            }
        }
    }

    private static async Task<List<Guid>> GetSessionIdsAsync(Guid userId)
    {
        await using var owner = OwnerContext();
        return await owner.UserSessions.Where(s => s.UserId == userId).Select(s => s.Id).ToListAsync();
    }

    private static async Task<List<string>> GetMarkersAsync(Guid userId)
    {
        await using var owner = OwnerContext();
        var rows = await owner.UserSessions.Where(s => s.UserId == userId).ToListAsync();
        return rows.Select(s =>
            s.RevokedAt is not null ? "revocada-hace-10d"
            : s.ExpiresAt > Now ? "viva-token-largo"
            : "vencida-pero-vista-hace-2d").ToList();
    }

    [PostgresFact]
    public async Task Purga_ExceptionLogs_SoloLosAnterioresAlCorte()
    {
        var marker = "H27-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await using (var owner = OwnerContext())
            {
                owner.ExceptionLogs.AddRange(
                    new ExceptionLog { Source = marker, ExceptionType = "X", Message = "vieja", CreatedAt = Now.AddDays(-100) },
                    new ExceptionLog { Source = marker, ExceptionType = "X", Message = "reciente", CreatedAt = Now.AddDays(-10) });
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);
            await using var appDb = AppContext(tenant);
            var job = Job(appDb, tenant, new DataRetentionOptions());

            await job.ApplyAsync(Now);

            await using var check = OwnerContext();
            var left = await check.ExceptionLogs.Where(l => l.Source == marker).Select(l => l.Message).ToListAsync();
            Assert.Equal(new[] { "reciente" }, left);
        }
        finally
        {
            await using var cleanup = OwnerContext();
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"ExceptionLogs\" WHERE \"Source\" = {0}", marker);
        }
    }

    // El GRANT por columna debe alcanzar para el WHERE de la purga y NADA más: la app sigue sin poder
    // leer el stack trace, el mensaje ni el correo (hallazgo #14).
    [PostgresFact]
    public async Task ToneApp_SoloPuedeLeerCreatedAt_DeExceptionLogs()
    {
        await using var conn = new Npgsql.NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await conn.OpenAsync();

        await using (var ok = new Npgsql.NpgsqlCommand("SELECT count(\"CreatedAt\") FROM \"ExceptionLogs\"", conn))
        {
            await ok.ExecuteScalarAsync(); // no debe lanzar
        }

        foreach (var column in new[] { "StackTrace", "Message", "UserEmail" })
        {
            await using var denied = new Npgsql.NpgsqlCommand($"SELECT \"{column}\" FROM \"ExceptionLogs\" LIMIT 1", conn);
            var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => denied.ExecuteScalarAsync());
            Assert.Equal("42501", ex.SqlState); // insufficient_privilege
        }
    }

    // AUDIT.md H-44: las fotos de evidencia vencidas pierden el ARCHIVO (Blob) pero conservan el registro.
    [PostgresFact]
    public async Task Evidencias_VencidasBorranElArchivo_YConservanLaFila_LasRecientesNoSeTocan()
    {
        var marker = "H44-" + Guid.NewGuid().ToString("N")[..8];
        Guid oldId = Guid.Empty, recentId = Guid.Empty;
        try
        {
            await using (var owner = OwnerContext())
            {
                var uploader = await owner.Users.Select(u => u.Id).FirstAsync();
                Toner.Domain.Entities.Evidence Photo(DateTime uploadedAt) => new()
                {
                    ClientId = RlsFixture.ClientA, Kind = Toner.Domain.Enums.EvidenceKind.Antes, SizeBytes = 10,
                    ServiceTicketId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
                    FileUrl = $"{marker}/{Guid.NewGuid()}.jpg", FileName = "f.jpg", ContentType = "image/jpeg",
                    UploadedByUserId = uploader, UploadedAt = uploadedAt
                };
                var old = Photo(Now.AddMonths(-4));
                var recent = Photo(Now.AddMonths(-1));
                owner.Evidences.AddRange(old, recent);
                await owner.SaveChangesAsync();
                oldId = old.Id;
                recentId = recent.Id;
            }

            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);
            var storage = new RecordingStorage();
            await using (var appDb = AppContext(tenant))
            {
                await Job(appDb, tenant, new DataRetentionOptions { EvidenceMonths = 3 }, storage).ApplyAsync(Now);
            }

            await using var check = OwnerContext();
            var rows = await check.Evidences.Where(e => e.FileUrl.StartsWith(marker)).ToListAsync();
            Assert.Equal(2, rows.Count); // las dos filas se conservan
            var purged = rows.Single(e => e.Id == oldId);
            Assert.NotNull(purged.ContentPurgedAt);
            Assert.Null(rows.Single(e => e.Id == recentId).ContentPurgedAt);
            Assert.Equal(new[] { purged.FileUrl }, storage.Deleted);

            // Idempotente: una segunda corrida no vuelve a borrar nada.
            var again = new RecordingStorage();
            await using (var appDb = AppContext(tenant))
            {
                await Job(appDb, tenant, new DataRetentionOptions { EvidenceMonths = 3 }, again).ApplyAsync(Now);
            }
            Assert.Empty(again.Deleted);
        }
        finally
        {
            await using var cleanup = OwnerContext();
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Evidences\" WHERE \"FileUrl\" LIKE {0}", marker + "%");
        }
    }

    [Fact]
    public void Opciones_PlazoMenorAlMinimo_FallaAlArrancar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DataRetentionOptions { UserSessionsDays = 0 }.EnsureValid());

        Assert.Contains("Retention:UserSessionsDays", ex.Message);
        Assert.Contains("Retention:EvidenceMonths",
            Assert.Throws<InvalidOperationException>(() => new DataRetentionOptions { EvidenceMonths = 0 }.EnsureValid()).Message);
        Assert.Contains("Retention:ExceptionLogsDays",
            Assert.Throws<InvalidOperationException>(() => new DataRetentionOptions { ExceptionLogsDays = 3 }.EnsureValid()).Message);
        new DataRetentionOptions().EnsureValid(); // el valor por defecto (90) es válido
    }
}
