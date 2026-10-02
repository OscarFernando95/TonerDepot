using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Toner.Application.Push;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Infrastructure.Push;

// Decide cuándo mandar una notificación push mirando lo que un SaveChanges realmente confirmó. Igual que
// RealtimeChangeInterceptor, está en el SaveChanges y no en cada servicio para cubrir de una vez cualquier camino
// que asigne o reasigne trabajo (endpoints, motor de asignación, cola de pendientes, jobs).
//
// Avisos:
//   - Al técnico: le asignan un ticket/orden, se lo quitan (reasignación) o se cancela uno que tenía.
//   - Al staff: un ticket queda sin asignar (el motor no encontró técnico disponible).
//
// Lo visible es genérico (puede verse con la pantalla bloqueada y la sesión cerrada) y los datos van solo con ids.
// Publicar nunca puede hacer fallar ni demorar el guardado: va en segundo plano y sus errores solo se registran.
public sealed class PushChangeInterceptor : SaveChangesInterceptor
{
    public const string EventAssigned = "assigned";
    public const string EventRemoved = "removed";
    public const string EventCancelled = "cancelled";
    public const string EventUnassigned = "unassigned";

    // Se resuelve al publicar y no al construir: el notificador depende de la fábrica de DbContext, y la fábrica
    // construye sus opciones con este interceptor — resolverlo en el constructor sería una dependencia circular
    // entre singletons (el arranque se queda colgado).
    private readonly Func<IPushNotifier> _notifierFactory;
    private readonly ILogger<PushChangeInterceptor> _logger;
    private readonly ConditionalWeakTable<DbContext, List<PushEvent>> _pending = new();

    public PushChangeInterceptor(Func<IPushNotifier> notifierFactory, ILogger<PushChangeInterceptor> logger)
    {
        _notifierFactory = notifierFactory;
        _logger = logger;
    }

    // Kind: "ticket" | "order". TechnicianId es null para los avisos al staff.
    private sealed record PushEvent(string Kind, Guid EntityId, string Event, Guid? TechnicianId);

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

        var events = _pending.GetOrCreateValue(context);
        events.Clear();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case ServiceTicket ticket:
                    CaptureAssignment(events, entry, "ticket", ticket.Id, ticket.TechnicianId,
                        nameof(ServiceTicket.TechnicianId), nameof(ServiceTicket.Status),
                        isCancelled: ticket.Status == ServiceTicketStatus.Cancelado,
                        becameUnassigned: ticket.Status == ServiceTicketStatus.SinAsignar);
                    break;
                case MaintenanceOrder order:
                    CaptureAssignment(events, entry, "order", order.Id, order.TechnicianId,
                        nameof(MaintenanceOrder.TechnicianId), nameof(MaintenanceOrder.Status),
                        isCancelled: order.Status == MaintenanceOrderStatus.Cancelada,
                        becameUnassigned: false);
                    break;
            }
        }
    }

    private static void CaptureAssignment(
        List<PushEvent> events, EntityEntry entry, string kind, Guid id, Guid? technicianId,
        string technicianProperty, string statusProperty, bool isCancelled, bool becameUnassigned)
    {
        var added = entry.State == EntityState.Added;
        var technician = entry.Property(technicianProperty);
        var technicianChanged = !added && technician.IsModified && !Equals(technician.OriginalValue, technician.CurrentValue);
        var statusChanged = !added && entry.Property(statusProperty).IsModified
            && !Equals(entry.Property(statusProperty).OriginalValue, entry.Property(statusProperty).CurrentValue);

        if (technicianId.HasValue && (added || technicianChanged))
        {
            events.Add(new(kind, id, EventAssigned, technicianId));
        }

        if (technicianChanged && technician.OriginalValue is Guid previous && previous != Guid.Empty)
        {
            events.Add(new(kind, id, EventRemoved, previous));
        }

        // Cancelado con técnico asignado (y sin cambio de técnico en el mismo guardado): el técnico debe enterarse.
        if (isCancelled && statusChanged && !technicianChanged && technicianId.HasValue)
        {
            events.Add(new(kind, id, EventCancelled, technicianId));
        }

        if (becameUnassigned && (added || statusChanged))
        {
            events.Add(new(kind, id, EventUnassigned, null));
        }
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
        if (context is null || !_pending.TryGetValue(context, out var events))
        {
            return;
        }

        _pending.Remove(context);
        var toSend = events.Distinct().ToList();
        if (toSend.Count == 0)
        {
            return;
        }

        // En segundo plano y sin propagar errores: avisar es un extra, jamás puede romper lo que ya se guardó.
        _ = Task.Run(async () =>
        {
            foreach (var e in toSend)
            {
                try
                {
                    var notifier = _notifierFactory();
                    var message = BuildMessage(e);
                    if (e.TechnicianId.HasValue)
                    {
                        await notifier.NotifyTechniciansAsync(new[] { e.TechnicianId.Value }, message);
                    }
                    else
                    {
                        await notifier.NotifyStaffAsync(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo enviar la notificación push de {Kind} {Event}", e.Kind, e.Event);
                }
            }
        });
    }

    private static PushMessage BuildMessage(PushEvent e)
    {
        var data = new Dictionary<string, string>
        {
            ["type"] = e.Kind,
            ["id"] = e.EntityId.ToString(),
            ["event"] = e.Event
        };

        var (title, body) = (e.Kind, e.Event) switch
        {
            ("ticket", EventAssigned) => ("Nuevo ticket asignado", "Abre Toner para ver los detalles."),
            ("order", EventAssigned) => ("Nueva orden de mantenimiento", "Abre Toner para ver los detalles."),
            (_, EventRemoved) => ("Cambio en tus asignaciones", "Un servicio que tenías ya no está a tu cargo."),
            (_, EventCancelled) => ("Servicio cancelado", "Un servicio asignado a ti fue cancelado."),
            (_, EventUnassigned) => ("Ticket sin asignar", "Hay un ticket esperando técnico."),
            _ => ("Toner", "Tienes novedades.")
        };

        return new PushMessage(title, body, data);
    }
}
