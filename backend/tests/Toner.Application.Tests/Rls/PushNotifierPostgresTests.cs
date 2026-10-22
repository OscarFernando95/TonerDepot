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
        public List<PushMessage> Messages { get; } = new();
        public HashSet<string> Dead { get; } = new();

        public Task<IReadOnlyList<PushOutcome>> SendAsync(IReadOnlyList<PushTarget> targets, PushMessage message, CancellationToken cancellationToken = default)
        {
            Sent.AddRange(targets);
            Messages.Add(message);
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

    // Datos sembrados por la prueba de resumen/instaladores; se limpian en orden inverso de dependencias.
    private static async Task CleanupSeedAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyCollection<Guid> cityIds, IReadOnlyCollection<Guid> zoneIds,
        Guid clientId, Guid brandId)
    {
        await using var cleanup = OwnerContext();
        async Task Sql(string sql, params object[] args) => await cleanup.Database.ExecuteSqlRawAsync(sql, args);
        await Sql("DELETE FROM \"ServiceTickets\" WHERE \"ClientId\" = {0}", clientId);
        await Sql("DELETE FROM \"Assets\" WHERE \"AssetModelId\" IN (SELECT \"Id\" FROM \"AssetModels\" WHERE \"AssetBrandId\" = {0})", brandId);
        await Sql("DELETE FROM \"AssetModels\" WHERE \"AssetBrandId\" = {0}", brandId);
        await Sql("DELETE FROM \"AssetBrands\" WHERE \"Id\" = {0}", brandId);
        await Sql("DELETE FROM \"ClientLocations\" WHERE \"ClientId\" = {0}", clientId);
        await Sql("DELETE FROM \"Clients\" WHERE \"Id\" = {0}", clientId);
        await Sql("DELETE FROM \"TechnicianZones\" WHERE \"ZoneId\" = ANY({0})", zoneIds.ToArray());
        await Sql("DELETE FROM \"Technicians\" WHERE \"UserId\" = ANY({0})", userIds.ToArray());
        await Sql("DELETE FROM \"Users\" WHERE \"Id\" = ANY({0})", userIds.ToArray());
        await Sql("DELETE FROM \"Cities\" WHERE \"Id\" = ANY({0})", cityIds.ToArray());
        await Sql("DELETE FROM \"Zones\" WHERE \"Id\" = ANY({0})", zoneIds.ToArray());
    }

    [PostgresFact]
    public async Task Instalacion_AvisaSoloALosTecnicosDeLaZonaDelEquipo_ConClienteCiudadYModelo()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-PUSHI-" + Guid.NewGuid().ToString("N")[..8];
        var userIds = new List<Guid>();
        var cityIds = new List<Guid>();
        var zoneIds = new List<Guid>();
        Guid clientId = default, brandId = default;
        try
        {
            await using var owner = OwnerContext();
            var role = await Rol(owner, RoleNames.Tecnico);
            var inZone = TestEntities.User(role);
            var otherZone = TestEntities.User(role);
            var inactive = TestEntities.User(role);
            var techIn = TestEntities.Technician(inZone);
            var techOther = TestEntities.Technician(otherZone);
            var techInactive = TestEntities.Technician(inactive, isActive: false);

            var cityA = TestEntities.City(marker + " Ciudad A");
            var cityB = TestEntities.City(marker + " Ciudad B");
            var coverageIn = TestEntities.Coverage(techIn, cityA);
            var coverageOther = TestEntities.Coverage(techOther, cityB);
            // El inactivo comparte la zona de cityA: cubre el municipio pero no debe recibir el aviso.
            var coverageInactive = new TechnicianZone { TechnicianId = techInactive.Id, ZoneId = coverageIn.ZoneId };

            var client = TestEntities.Client(marker + " Cliente");
            var location = TestEntities.ClientLocation(client, cityA);
            var brand = TestEntities.AssetBrand(marker + " Marca");
            var model = TestEntities.AssetModel(brand, "MP-X1");
            var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);
            clientId = client.Id;
            brandId = brand.Id;

            owner.AddRange(inZone, otherZone, inactive, techIn, techOther, techInactive, cityA, cityB, client, location, brand, model);
            owner.AddRange(coverageIn, coverageOther, coverageInactive);
            await owner.SaveChangesAsync();
            owner.Add(asset);
            await owner.SaveChangesAsync();
            userIds.AddRange(new[] { inZone.Id, otherZone.Id, inactive.Id });
            cityIds.AddRange(new[] { cityA.Id, cityB.Id });
            zoneIds.AddRange(new[] { coverageIn.ZoneId, coverageOther.ZoneId });
            owner.DeviceTokens.AddRange(
                new DeviceToken { UserId = inZone.Id, Platform = PushPlatform.Android, Token = marker + "-in" },
                new DeviceToken { UserId = otherZone.Id, Platform = PushPlatform.Android, Token = marker + "-other" },
                new DeviceToken { UserId = inactive.Id, Platform = PushPlatform.Android, Token = marker + "-off" });
            await owner.SaveChangesAsync();

            var transport = new FakeTransport();
            var notifier = new PushNotifier(new AppRoleFactory(new TenantContextAccessor()), new TenantContextAccessor(), transport, NullLogger<PushNotifier>.Instance);
            var generic = new PushMessage("Instalación disponible", "genérico", new Dictionary<string, string>
            {
                ["type"] = "installation", ["id"] = asset.Id.ToString(), ["event"] = "available"
            });

            await notifier.NotifyInstallersAsync(asset.Id, generic);

            Assert.Equal(new[] { marker + "-in" }, transport.Sent.Select(s => s.Token).Where(t => t.StartsWith(marker)));
            var body = Assert.Single(transport.Messages).Body;
            Assert.Equal($"{marker} Cliente · {marker} Ciudad A\n{marker} Marca MP-X1", body);
            Assert.DoesNotContain("Criticidad", body); // las instalaciones no tienen criticidad
        }
        finally
        {
            await CleanupSeedAsync(userIds, cityIds, zoneIds, clientId, brandId);
        }
    }

    [PostgresFact]
    public async Task Ticket_LlevaClienteCiudadCriticidadModeloYArea_YUnEquipoYaInstaladoNoAvisaInstaladores()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-PUSHT-" + Guid.NewGuid().ToString("N")[..8];
        var userIds = new List<Guid>();
        var cityIds = new List<Guid>();
        var zoneIds = new List<Guid>();
        Guid clientId = default, brandId = default;
        try
        {
            await using var owner = OwnerContext();
            var role = await Rol(owner, RoleNames.Tecnico);
            var techUser = TestEntities.User(role);
            var tech = TestEntities.Technician(techUser);
            var city = TestEntities.City(marker + " Ciudad");
            var coverage = TestEntities.Coverage(tech, city);
            var client = TestEntities.Client(marker + " Cliente");
            var location = TestEntities.ClientLocation(client, city);
            var brand = TestEntities.AssetBrand(marker + " Marca");
            var model = TestEntities.AssetModel(brand, "MP-X2");
            var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
            asset.Area = "Contabilidad";
            clientId = client.Id;
            brandId = brand.Id;

            owner.AddRange(techUser, tech, city, coverage, client, location, brand, model);
            await owner.SaveChangesAsync();
            owner.Add(asset);
            await owner.SaveChangesAsync();
            var ticket = TestEntities.ServiceTicket(location, techUser, ServiceTicketStatus.Asignado, ServiceTicketPriority.Critica, tech.Id);
            ticket.ClientId = client.Id;
            ticket.AssetId = asset.Id;
            owner.Add(ticket);
            userIds.Add(techUser.Id);
            cityIds.Add(city.Id);
            zoneIds.Add(coverage.ZoneId);
            owner.DeviceTokens.Add(new DeviceToken { UserId = techUser.Id, Platform = PushPlatform.Android, Token = marker + "-t" });
            await owner.SaveChangesAsync();

            var transport = new FakeTransport();
            var notifier = new PushNotifier(new AppRoleFactory(new TenantContextAccessor()), new TenantContextAccessor(), transport, NullLogger<PushNotifier>.Instance);
            var generic = new PushMessage("Nuevo ticket asignado", "genérico", new Dictionary<string, string>
            {
                ["type"] = "ticket", ["id"] = ticket.Id.ToString(), ["event"] = "assigned"
            });

            await notifier.NotifyTechniciansAsync(new[] { tech.Id }, generic);

            var body = Assert.Single(transport.Messages).Body;
            Assert.Equal($"{marker} Cliente · {marker} Ciudad\nCriticidad Crítica · {marker} Marca MP-X2 · Área Contabilidad", body);

            // Un equipo que ya no está pendiente de instalar no genera avisos de instalación.
            transport.Sent.Clear();
            transport.Messages.Clear();
            await notifier.NotifyInstallersAsync(asset.Id, generic);
            Assert.Empty(transport.Sent);
        }
        finally
        {
            await CleanupSeedAsync(userIds, cityIds, zoneIds, clientId, brandId);
        }
    }

    private static async Task<Role> Rol(TonerDbContext db, string name)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == name) ?? TestEntities.Role(name);
        if (db.Entry(role).State == EntityState.Detached) db.Add(role);
        return role;
    }
}
