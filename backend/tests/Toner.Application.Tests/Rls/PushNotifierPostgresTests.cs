using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Push;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;
using Toner.Infrastructure.Push;

namespace Toner.Application.Tests.Rls;

// El notificador corre en segundo plano con el rol de la aplicación (no el owner) y fuera de cualquier request: aquí se
// comprueba contra el Postgres real que con contexto de staff llega a técnicos, usuarios y tokens, y que los índices
// únicos de DeviceTokens se cumplen en la base y no solo en el servicio.
[Collection(nameof(RlsFixtureCollection))]
public class PushNotifierPostgresTests
{
    private readonly RlsFixture _fixture;

    public PushNotifierPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private sealed class AppRoleFactory : IDbContextFactory<TonerDbContext>
    {
        private readonly TenantContextAccessor _tenant;
        public AppRoleFactory(TenantContextAccessor tenant) => _tenant = tenant;

        public TonerDbContext CreateDbContext() => new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(_tenant))
            .Options);
    }

    private sealed class FakeTransport : IPushTransport
    {
        public bool IsEnabled => true;
        public List<PushTarget> Sent { get; } = new();
        public HashSet<string> Dead { get; } = new();

        public Task<IReadOnlyList<PushOutcome>> SendAsync(IReadOnlyList<PushTarget> targets, PushMessage message, CancellationToken cancellationToken = default)
        {
            Sent.AddRange(targets);
            return Task.FromResult<IReadOnlyList<PushOutcome>>(
                targets.Select(t => Dead.Contains(t.Token) ? PushOutcome.InvalidToken : PushOutcome.Sent).ToList());
        }
    }

    private static readonly PushMessage Message = new("t", "b", new Dictionary<string, string>());

    [PostgresFact]
    public async Task Tecnicos_RecibeSoloLosActivosConToken_YRetiraLosTokensCaducados()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-PUSH-" + Guid.NewGuid().ToString("N")[..8];
        var userIds = new List<Guid>();
        try
        {
            await using var owner = OwnerContext();
            var role = await owner.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Tecnico) ?? TestEntities.Role(RoleNames.Tecnico);
            if (owner.Entry(role).State == EntityState.Detached) owner.Add(role);
            var active = TestEntities.User(role);
            var inactiveTech = TestEntities.User(role);
            var deadToken = TestEntities.User(role);
            var techActive = TestEntities.Technician(active);
            var techInactive = TestEntities.Technician(inactiveTech, isActive: false);
            var techDead = TestEntities.Technician(deadToken);
            owner.AddRange(active, inactiveTech, deadToken, techActive, techInactive, techDead);
            userIds.AddRange(new[] { active.Id, inactiveTech.Id, deadToken.Id });
            owner.DeviceTokens.AddRange(
                new DeviceToken { UserId = active.Id, Platform = PushPlatform.Android, Token = marker + "-a" },
                new DeviceToken { UserId = active.Id, Platform = PushPlatform.iOS, Token = marker + "-i" },
                new DeviceToken { UserId = inactiveTech.Id, Platform = PushPlatform.Android, Token = marker + "-x" },
                new DeviceToken { UserId = deadToken.Id, Platform = PushPlatform.Android, Token = marker + "-dead" });
            await owner.SaveChangesAsync();

            var transport = new FakeTransport();
            transport.Dead.Add(marker + "-dead");
            var notifier = new PushNotifier(new AppRoleFactory(new TenantContextAccessor()), new TenantContextAccessor(), transport, NullLogger<PushNotifier>.Instance);

            await notifier.NotifyTechniciansAsync(new[] { techActive.Id, techInactive.Id, techDead.Id }, Message);

            Assert.Equivalent(new[] { marker + "-a", marker + "-i", marker + "-dead" }, transport.Sent.Select(s => s.Token), strict: true);
            await using var check = OwnerContext();
            Assert.False(await check.DeviceTokens.AnyAsync(d => d.Token == marker + "-dead"));
            Assert.True(await check.DeviceTokens.AnyAsync(d => d.Token == marker + "-a"));
        }
        finally
        {
            await using var cleanup = OwnerContext();
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Technicians\" WHERE \"UserId\" = ANY({0})", userIds.ToArray());
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Id\" = ANY({0})", userIds.ToArray());
        }
    }

    [PostgresFact]
    public async Task Staff_RecibeAdminsYCoordinadoresActivos_NuncaTecnicosNiClientes()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-PUSHS-" + Guid.NewGuid().ToString("N")[..8];
        var userIds = new List<Guid>();
        try
        {
            await using var owner = OwnerContext();
            var admin = await Rol(owner, RoleNames.Administrador);
            var tech = await Rol(owner, RoleNames.Tecnico);
            var u1 = TestEntities.User(admin);
            var u2 = TestEntities.User(tech);
            var u3 = TestEntities.User(admin);
            u3.IsActive = false;
            owner.AddRange(u1, u2, u3);
            userIds.AddRange(new[] { u1.Id, u2.Id, u3.Id });
            owner.DeviceTokens.AddRange(
                new DeviceToken { UserId = u1.Id, Platform = PushPlatform.Android, Token = marker + "-admin" },
                new DeviceToken { UserId = u2.Id, Platform = PushPlatform.Android, Token = marker + "-tech" },
                new DeviceToken { UserId = u3.Id, Platform = PushPlatform.Android, Token = marker + "-off" });
            await owner.SaveChangesAsync();

            var transport = new FakeTransport();
            var notifier = new PushNotifier(new AppRoleFactory(new TenantContextAccessor()), new TenantContextAccessor(), transport, NullLogger<PushNotifier>.Instance);
            await notifier.NotifyStaffAsync(Message);

            var mine = transport.Sent.Select(s => s.Token).Where(t => t.StartsWith(marker)).ToList();
            Assert.Equal(new[] { marker + "-admin" }, mine);
        }
        finally
        {
            await using var cleanup = OwnerContext();
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Id\" = ANY({0})", userIds.ToArray());
        }
    }

    [PostgresFact]
    public async Task Indices_LaBaseImpideDosTokensDeLaMismaPlataformaOElMismoTokenEnDosUsuarios()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-PUSHU-" + Guid.NewGuid().ToString("N")[..8];
        var userIds = new List<Guid>();
        try
        {
            await using var owner = OwnerContext();
            var tech = await Rol(owner, RoleNames.Tecnico);
            var a = TestEntities.User(tech);
            var b = TestEntities.User(tech);
            owner.AddRange(a, b);
            userIds.AddRange(new[] { a.Id, b.Id });
            owner.DeviceTokens.Add(new DeviceToken { UserId = a.Id, Platform = PushPlatform.Android, Token = marker + "-1" });
            await owner.SaveChangesAsync();

            await using var samePlatform = OwnerContext();
            samePlatform.DeviceTokens.Add(new DeviceToken { UserId = a.Id, Platform = PushPlatform.Android, Token = marker + "-2" });
            await Assert.ThrowsAsync<Toner.Application.Common.Exceptions.ConflictException>(() => samePlatform.SaveChangesAsync());

            await using var sameToken = OwnerContext();
            sameToken.DeviceTokens.Add(new DeviceToken { UserId = b.Id, Platform = PushPlatform.Android, Token = marker + "-1" });
            await Assert.ThrowsAsync<Toner.Application.Common.Exceptions.ConflictException>(() => sameToken.SaveChangesAsync());
        }
        finally
        {
            await using var cleanup = OwnerContext();
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Id\" = ANY({0})", userIds.ToArray());
        }
    }

    private static async Task<Role> Rol(TonerDbContext db, string name)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == name) ?? TestEntities.Role(name);
        if (db.Entry(role).State == EntityState.Detached) db.Add(role);
        return role;
    }
}
