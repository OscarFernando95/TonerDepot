using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Toner.Application.Common.Interfaces;
using Toner.Application.Push;
using Toner.Domain.Common;
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
