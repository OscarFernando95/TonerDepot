using System.Collections.Concurrent;

namespace Toner.Api.Hubs;

// Qué conexiones en vivo pertenecen a cada sesión, y a qué grupos se unió cada una — para poder cortar el canal
// cuando la sesión se revoca (una conexión abierta no vuelve a pasar por la validación del token).
public sealed class RealtimeConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, string[]>> _bySession = new();
    private readonly ConcurrentDictionary<string, Guid> _sessionByConnection = new();

    public void Add(Guid sessionId, string connectionId, IEnumerable<string> groups)
    {
        _bySession.GetOrAdd(sessionId, _ => new()).TryAdd(connectionId, groups.ToArray());
        _sessionByConnection[connectionId] = sessionId;
    }

    public void Remove(string connectionId)
    {
        if (_sessionByConnection.TryRemove(connectionId, out var sessionId) && _bySession.TryGetValue(sessionId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty)
            {
                _bySession.TryRemove(sessionId, out _);
            }
        }
    }

    // Conexiones (con sus grupos) de una sesión, para sacarlas de esos grupos.
    public IReadOnlyList<(string ConnectionId, string[] Groups)> ConnectionsOf(Guid sessionId) =>
        _bySession.TryGetValue(sessionId, out var connections)
            ? connections.Select(c => (c.Key, c.Value)).ToList()
            : Array.Empty<(string, string[])>();
}
