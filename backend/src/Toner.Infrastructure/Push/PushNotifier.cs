using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Toner.Application.Common.Interfaces;
using Toner.Application.Push;
using Toner.Domain.Common;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Infrastructure.Push;

// Resuelve a quién avisar (técnicos o staff), busca sus tokens y los envía por FCM. Singleton y con su propio
// DbContext (vía fábrica): lo invoca el interceptor en segundo plano, ya fuera de la request que originó el cambio.
// Enviar un aviso jamás puede hacer fallar lo que ya se guardó: los errores solo se registran.
public sealed class PushNotifier : IPushNotifier
{
    private readonly IDbContextFactory<TonerDbContext> _dbFactory;
    private readonly ITenantContextAccessor _tenant;
    private readonly IPushTransport _transport;
    private readonly ILogger<PushNotifier> _logger;

    public PushNotifier(
        IDbContextFactory<TonerDbContext> dbFactory,
        ITenantContextAccessor tenant,
        IPushTransport transport,
        ILogger<PushNotifier> logger)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _transport = transport;
        _logger = logger;
    }

    public async Task NotifyTechniciansAsync(
        IReadOnlyCollection<Guid> technicianIds, PushMessage message, CancellationToken cancellationToken = default)
    {
        if (!_transport.IsEnabled || technicianIds.Count == 0)
        {
            return;
        }

        using var tenantScope = _tenant.Push(() => TenantContext.Staff);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var userIds = await db.Technicians
            .Where(t => technicianIds.Contains(t.Id) && t.IsActive && t.User.IsActive)
            .Select(t => t.UserId)
            .ToListAsync(cancellationToken);

        await SendToUsersAsync(db, userIds, message, cancellationToken);
    }

    public async Task NotifyStaffAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        if (!_transport.IsEnabled)
        {
            return;
        }

        using var tenantScope = _tenant.Push(() => TenantContext.Staff);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var userIds = await db.Users
            .Where(u => u.IsActive && (u.Role.Name == RoleNames.Administrador || u.Role.Name == RoleNames.Coordinador))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        await SendToUsersAsync(db, userIds, message, cancellationToken);
    }

    public async Task NotifyInstallersAsync(Guid assetId, PushMessage message, CancellationToken cancellationToken = default)
    {
        if (!_transport.IsEnabled)
        {
            return;
        }

        using var tenantScope = _tenant.Push(() => TenantContext.Staff);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var cityId = await db.Assets
            .Where(a => a.Id == assetId && a.LifecycleStatus == AssetLifecycleStatus.PendienteInstalacion
                && a.CurrentClientLocation != null)
            .Select(a => (Guid?)a.CurrentClientLocation!.CityId)
            .FirstOrDefaultAsync(cancellationToken);
        if (cityId is null)
        {
            return;
        }

        // Misma cobertura que ve el técnico en su lista de instalaciones pendientes (ver CoverageQueries.CoveredCityIds).
        var userIds = await db.Technicians
            .Where(t => t.IsActive && t.User.IsActive
                && t.TechnicianZones.Any(tz => tz.Zone.Cities.Any(c => c.Id == cityId)))
            .Select(t => t.UserId)
            .ToListAsync(cancellationToken);

        await SendToUsersAsync(db, userIds, message, cancellationToken);
    }

    // Resumen operativo del servicio al que apunta el aviso. Enriquecer es un extra: si la consulta falla, el aviso sale
    // genérico en vez de perderse.
    private async Task<PushMessage> EnrichAsync(TonerDbContext db, PushMessage message, CancellationToken cancellationToken)
    {
        try
        {
            if (!message.Data.TryGetValue("type", out var type)
                || !message.Data.TryGetValue("id", out var rawId)
                || !Guid.TryParse(rawId, out var id))
            {
                return message;
            }

            PushDetail? detail = type switch
            {
                "ticket" => await db.ServiceTickets
                    .Where(t => t.Id == id)
                    .Select(t => new
                    {
                        Client = t.ClientLocation.Client.Name,
                        City = t.ClientLocation.City.Name,
                        t.Priority,
                        Brand = t.Asset != null ? t.Asset.AssetModel.AssetBrand.Name : t.ExternalAssetBrand,
                        Model = t.Asset != null ? t.Asset.AssetModel.Name : t.ExternalAssetModel,
                        Area = t.Asset != null ? t.Asset.Area : null
                    })
                    .Select(x => new PushDetail(x.Client, x.City, x.Priority, PushDetail.JoinEquipment(x.Brand, x.Model), x.Area))
                    .FirstOrDefaultAsync(cancellationToken),
                "order" => await AssetDetailAsync(db, db.MaintenanceOrders.Where(o => o.Id == id).Select(o => o.AssetId), cancellationToken),
                "installation" => await AssetDetailAsync(db, db.Assets.Where(a => a.Id == id).Select(a => a.Id), cancellationToken),
                _ => null
            };

            return detail?.ApplyTo(message) ?? message;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo armar el resumen de la notificación push; se envía el texto genérico");
            return message;
        }
    }

    // Órdenes e instalaciones: no tienen criticidad; el cliente y la ciudad salen de la sede actual del equipo.
    private static async Task<PushDetail?> AssetDetailAsync(
        TonerDbContext db, IQueryable<Guid> assetIds, CancellationToken cancellationToken)
    {
        var row = await db.Assets
            .Where(a => assetIds.Contains(a.Id))
            .Select(a => new
            {
                Client = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Name : null,
                City = a.CurrentClientLocation != null ? a.CurrentClientLocation.City.Name : null,
                Brand = a.AssetModel.AssetBrand.Name,
                Model = a.AssetModel.Name,
                a.Area
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new PushDetail(row.Client, row.City, null, PushDetail.JoinEquipment(row.Brand, row.Model), row.Area);
    }

    private async Task SendToUsersAsync(
        TonerDbContext db, IReadOnlyCollection<Guid> userIds, PushMessage message, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var tokens = await db.DeviceTokens
            .Where(d => userIds.Contains(d.UserId))
            .ToListAsync(cancellationToken);
        if (tokens.Count == 0)
        {
            return;
        }

        message = await EnrichAsync(db, message, cancellationToken);

        var outcomes = await _transport.SendAsync(
            tokens.Select(t => new PushTarget(t.Token, t.Platform)).ToList(), message, cancellationToken);

        var dead = tokens.Where((_, i) => outcomes[i] == PushOutcome.InvalidToken).ToList();
        if (dead.Count > 0)
        {
            db.DeviceTokens.RemoveRange(dead);
            await db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Push enviado: {Sent} de {Total} dispositivo(s), {Removed} token(s) caducado(s) retirado(s)",
            outcomes.Count(o => o == PushOutcome.Sent), outcomes.Count, dead.Count);
    }
}
