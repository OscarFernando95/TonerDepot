using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Common;

// Alcance por sede de un usuario Cliente. RLS aísla por EMPRESA (ver UserClientLocation); que un cliente vea solo algunas
// sedes de la suya lo cumple la aplicación con este filtro, que cada consulta de cliente (tickets, activos, mantenimientos,
// sedes) aplica AND con el aislamiento por empresa.
public static class ClientScope
{
    // null  → sin restricción por sede (staff, técnico, o cliente con todas las sedes de su empresa).
    // lista → solo esas sedes. Una lista vacía significa "ninguna": restringido sin sedes falla cerrado.
    public static async Task<IReadOnlyList<Guid>?> AllowedLocationIdsAsync(
        this IApplicationDbContext db, RequestingUser user, CancellationToken cancellationToken = default)
    {
        if (!user.IsClient)
        {
            return null;
        }

        var clientId = user.RequireClientId();
        var row = await db.Users
            .Where(u => u.Id == user.UserId)
            .Select(u => new
            {
                u.RestrictedToLocations,
                // También por ClientId: una fila de otra empresa nunca cuenta, aunque RLS ya la oculte.
                Ids = u.AllowedLocations.Where(l => l.ClientId == clientId).Select(l => l.ClientLocationId).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Usuario inexistente: no se puede saber qué ve, así que no ve nada.
        if (row is null)
        {
            return Array.Empty<Guid>();
        }

        return row.RestrictedToLocations ? row.Ids : null;
    }
}
