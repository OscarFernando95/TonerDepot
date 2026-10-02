using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Toner.Application.Realtime;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Realtime;

// Publica un aviso en vivo por cada entidad relevante que un SaveChanges realmente confirmó. Está en el
// SaveChanges (y no en cada servicio) a propósito: cubre de una vez cualquier camino de escritura — endpoints, jobs
// de Hangfire, el motor de asignación — sin depender de que quien escribe un servicio nuevo se acuerde de avisar.
//
// Se captura ANTES de guardar (el estado de cada entrada todavía dice qué cambió) y se publica DESPUÉS, solo si el
// guardado tuvo éxito. Publicar nunca puede hacer fallar ni demorar el guardado: va en segundo plano y sus errores
// solo se registran.
public sealed class RealtimeChangeInterceptor : SaveChangesInterceptor
{
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<RealtimeChangeInterceptor> _logger;
    private readonly ConditionalWeakTable<DbContext, Pending> _pending = new();

    public RealtimeChangeInterceptor(IRealtimeNotifier notifier, ILogger<RealtimeChangeInterceptor> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    private sealed class Pending
    {
        public List<EntityChange> Changes { get; } = new();
        public List<Guid> RevokedSessions { get; } = new();
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Publish(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Publish(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var pending = _pending.GetOrCreateValue(context);
        pending.Changes.Clear();
        pending.RevokedSessions.Clear();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "created",
                EntityState.Deleted => "deleted",
                _ => "updated"
            };

            switch (entry.Entity)
            {
                case ServiceTicket ticket:
                    pending.Changes.Add(new("Ticket", ticket.Id, action, ticket.ClientId, Technicians(entry, ticket.TechnicianId, nameof(ServiceTicket.TechnicianId))));
                    break;
                case MaintenanceOrder order:
                    pending.Changes.Add(new("MaintenanceOrder", order.Id, action, order.ClientId, Technicians(entry, order.TechnicianId, nameof(MaintenanceOrder.TechnicianId))));
                    break;
                case TimeLog log:
                    pending.Changes.Add(new("Visit", log.Id, action, log.ClientId, new[] { log.TechnicianId }));
                    break;
                case Technician technician:
                    pending.Changes.Add(new("Technician", technician.Id, action, null, new[] { technician.Id }));
                    break;
                case TechnicianTimeOff timeOff:
                    pending.Changes.Add(new("Technician", timeOff.TechnicianId, "updated", null, new[] { timeOff.TechnicianId }));
                    break;
                case TechnicianWorkInterval interval:
                    pending.Changes.Add(new("Technician", interval.TechnicianId, "updated", null, new[] { interval.TechnicianId }));
                    break;
                case Evidence evidence:
                    pending.Changes.Add(new("Evidence", evidence.Id, action, evidence.ClientId, Array.Empty<Guid>()));
                    break;
                case Asset asset:
                    pending.Changes.Add(new("Asset", asset.Id, action, asset.ClientId, Array.Empty<Guid>()));
                    break;
                case Contract contract:
                    pending.Changes.Add(new("Contract", contract.Id, action, contract.ClientId, Array.Empty<Guid>()));
                    break;
                case Client client:
                    pending.Changes.Add(new("Client", client.Id, action, client.Id, Array.Empty<Guid>()));
                    break;
                case ClientLocation location:
                    pending.Changes.Add(new("Client", location.ClientId, "updated", location.ClientId, Array.Empty<Guid>()));
                    break;
                case MaintenanceSchedule schedule:
                    pending.Changes.Add(new("Schedule", schedule.Id, action, schedule.ClientId, Array.Empty<Guid>()));
                    break;
                case MeterReading reading:
                    pending.Changes.Add(new("MeterReading", reading.Id, action, reading.ClientId, Array.Empty<Guid>()));
                    break;
                case CompanyHolidayOverride holiday:
                    pending.Changes.Add(new("Holiday", holiday.Id, action, null, Array.Empty<Guid>()));
                    break;
                case TechnicianAsset link:
                    pending.Changes.Add(new("TechnicianAsset", link.Id, action, null, new[] { link.TechnicianId }));
                    break;
                case ContractAsset contractAsset:
                    pending.Changes.Add(new("Contract", contractAsset.ContractId, "updated", contractAsset.ClientId, Array.Empty<Guid>()));
                    break;
                case User user:
                    pending.Changes.Add(new("User", user.Id, action, null, Array.Empty<Guid>()));
                    break;
                case UserSession session when entry.State == EntityState.Modified
                    && entry.Property(nameof(UserSession.RevokedAt)).IsModified
                    && session.RevokedAt is not null:
                    pending.RevokedSessions.Add(session.Id);
                    break;
            }
        }
    }

    // El técnico actual y, si la asignación cambió, también el anterior (para que su pantalla se actualice).
    private static Guid[] Technicians(EntityEntry entry, Guid? current, string property)
    {
        var ids = new List<Guid>();
        if (current.HasValue)
        {
            ids.Add(current.Value);
        }

        if (entry.State == EntityState.Modified && entry.Property(property).IsModified
            && entry.Property(property).OriginalValue is Guid previous && previous != Guid.Empty)
        {
            ids.Add(previous);
        }

        return ids.Distinct().ToArray();
    }

    private void Discard(DbContext? context)
    {
        if (context is not null)
        {
            _pending.Remove(context);
        }
    }

    private void Publish(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
        {
            return;
        }

        _pending.Remove(context);

        // Un mismo save puede tocar varias veces la misma entidad: se colapsa para no repetir el aviso.
        var changes = pending.Changes
            .GroupBy(c => (c.Entity, c.Id))
            .Select(g => g.First() with
            {
                Action = g.Any(c => c.Action == "created") ? "created" : g.First().Action,
                TechnicianIds = g.SelectMany(c => c.TechnicianIds).Distinct().ToArray()
            })
            .ToList();
        var revoked = pending.RevokedSessions.Distinct().ToList();

        if (changes.Count == 0 && revoked.Count == 0)
        {
            return;
        }

        // En segundo plano y sin propagar errores: avisar en vivo es un extra, jamás puede romper lo que ya se guardó.
        _ = Task.Run(async () =>
        {
            try
            {
                if (changes.Count > 0)
                {
                    await _notifier.PublishAsync(changes);
                }

                if (revoked.Count > 0)
                {
                    await _notifier.SessionsRevokedAsync(revoked);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo publicar el aviso en vivo de {Count} cambio(s)", changes.Count);
            }
        });
    }
}
