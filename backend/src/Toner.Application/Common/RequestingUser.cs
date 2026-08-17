using Toner.Application.Common.Exceptions;
using Toner.Domain.Common;

namespace Toner.Application.Common;

// Contexto del usuario autenticado que hace la llamada, para autorización de grano fino
// (ej. un Cliente solo puede ver/crear tickets de su propio ClientId, un Tecnico solo ve lo suyo)
// que no cabe en [Authorize(Roles=...)].
public record RequestingUser(Guid UserId, string Role, Guid? ClientId, Guid? TechnicianId)
{
    public bool IsStaff => Role is RoleNames.Administrador or RoleNames.Coordinador;
    public bool IsTechnician => Role is RoleNames.Tecnico;

    // Usar en vez de comparar contra ClientId directamente en un filtro/chequeo (SECURITY_AUDIT.md
    // hallazgo #20). Antes, un ClientId nulo en un usuario que debería tenerlo (rol Cliente mal
    // formado) caía en comparaciones tipo "columna == null", que hoy por casualidad no matchean nada
    // porque esas columnas son NOT NULL — un fail-open implícito, no una denegación garantizada por
    // el código. Esto lo hace explícito: sin ClientId, se deniega, punto.
    public Guid RequireClientId() =>
        ClientId ?? throw new ForbiddenException("Tu usuario no tiene un cliente asociado.");
}
