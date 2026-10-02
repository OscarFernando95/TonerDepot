using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Toner.Domain.Common;

namespace Toner.Api.Hubs;

// Canal en vivo (WebSocket) por el que el servidor avisa "esto cambió" — ver IRealtimeNotifier. Es de un solo
// sentido: el cliente no invoca nada; solo escucha `entityChanged` y `sessionRevoked`.
//
// A qué grupos se une cada conexión lo decide el servidor a partir del token, nunca el cliente:
//   staff            → Administrador y Coordinador (ven todo lo que la API les deja ver)
//   tech:{id}        → un Tecnico, solo lo suyo
//   technicians      → todos los Tecnicos (festivos, lecturas de contador: cosas compartidas por el equipo)
//   client:{id}      → un Cliente, solo lo de su empresa
//   session:{jti}    → para poder cortar el canal cuando la sesión se revoca
[Authorize(Roles = $"{RoleNames.Administrador},{RoleNames.Coordinador},{RoleNames.Tecnico},{RoleNames.Cliente}")]
public class UpdatesHub : Hub
{
    private readonly RealtimeConnectionTracker _tracker;

    public UpdatesHub(RealtimeConnectionTracker tracker)
    {
        _tracker = tracker;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        var groups = new List<string>();

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (role is RoleNames.Administrador or RoleNames.Coordinador)
        {
            groups.Add(RealtimeGroups.Staff);
        }
        else if (role == RoleNames.Tecnico && Guid.TryParse(user.FindFirstValue("technician_id"), out var technicianId))
        {
            groups.Add(RealtimeGroups.Technician(technicianId));
            groups.Add(RealtimeGroups.AllTechnicians);
        }
        else if (role == RoleNames.Cliente && Guid.TryParse(user.FindFirstValue("client_id"), out var clientId))
        {
            groups.Add(RealtimeGroups.Client(clientId));
        }

        // Un rol sin grupos (p. ej. un Cliente sin client_id válido) no recibe nada: fail-closed.
        if (Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Jti), out var sessionId))
        {
            groups.Add(RealtimeGroups.Session(sessionId));
            _tracker.Add(sessionId, Context.ConnectionId, groups);
        }

        foreach (var group in groups)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _tracker.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

public static class RealtimeGroups
{
    public const string Staff = "staff";
    public const string AllTechnicians = "technicians";
    public static string Technician(Guid id) => $"tech:{id}";
    public static string Client(Guid id) => $"client:{id}";
    public static string Session(Guid id) => $"session:{id}";
}
