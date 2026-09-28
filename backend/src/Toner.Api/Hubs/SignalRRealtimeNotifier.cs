using Microsoft.AspNetCore.SignalR;
using Toner.Application.Realtime;

namespace Toner.Api.Hubs;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<UpdatesHub> _hub;
    private readonly RealtimeConnectionTracker _tracker;

    public SignalRRealtimeNotifier(IHubContext<UpdatesHub> hub, RealtimeConnectionTracker tracker)
    {
        _hub = hub;
        _tracker = tracker;
    }

    public async Task PublishAsync(IReadOnlyList<EntityChange> changes, CancellationToken cancellationToken = default)
    {
        foreach (var change in changes)
        {
            var groups = RealtimeAudience.GroupsFor(change);
            if (groups.Count == 0)
            {
                continue;
            }

            // Solo identificadores: el receptor vuelve a pedir el recurso por la API con su autorización de siempre.
            var payload = new { entity = change.Entity, id = change.Id, action = change.Action };
            await _hub.Clients.Groups(groups).SendAsync("entityChanged", payload, cancellationToken);
        }
    }

    public async Task SessionsRevokedAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken = default)
    {
        foreach (var sessionId in sessionIds)
        {
            foreach (var (connectionId, groups) in _tracker.ConnectionsOf(sessionId))
            {
                // Primero se le quitan los grupos (deja de recibir datos) y después se le avisa por su cuenta.
                foreach (var group in groups)
                {
                    await _hub.Groups.RemoveFromGroupAsync(connectionId, group, cancellationToken);
                }

                await _hub.Clients.Client(connectionId).SendAsync("sessionRevoked", cancellationToken);
            }
        }
    }
}

public static class RealtimeAudience
{
    // Quién se entera de cada cambio. El staff ve todo; un técnico solo lo suyo; un cliente solo lo de su empresa.
    // Cosas internas (visitas, técnicos, evidencia, cronogramas, usuarios) nunca van a un cliente.
    public static List<string> GroupsFor(EntityChange change)
    {
        var groups = new List<string> { RealtimeGroups.Staff };

        foreach (var technicianId in change.TechnicianIds)
        {
            groups.Add(RealtimeGroups.Technician(technicianId));
        }

        var clientVisible = change.Entity is "Ticket" or "MaintenanceOrder" or "Asset" or "Contract" or "Client";
        if (clientVisible && change.ClientId is { } clientId && clientId != Guid.Empty)
        {
            groups.Add(RealtimeGroups.Client(clientId));
        }

        return groups.Distinct().ToList();
    }
}
