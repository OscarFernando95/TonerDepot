using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
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
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

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
            var job = new DataRetentionJob(
                appDb, Options.Create(new DataRetentionOptions { UserSessionsDays = 90 }),
                new NoopExceptionLogger(), NullLogger<DataRetentionJob>.Instance, tenant);

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

    [Fact]
    public void Opciones_PlazoMenorAlMinimo_FallaAlArrancar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DataRetentionOptions { UserSessionsDays = 0 }.EnsureValid());

        Assert.Contains("Retention:UserSessionsDays", ex.Message);
        new DataRetentionOptions().EnsureValid(); // el valor por defecto (90) es válido
    }
}
