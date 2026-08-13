using Toner.Domain.Common;

namespace Toner.Application.Common;

// Contexto del usuario autenticado que hace la llamada, para autorización de grano fino
// (ej. un Cliente solo puede ver/crear tickets de su propio ClientId, un Tecnico solo ve lo suyo)
// que no cabe en [Authorize(Roles=...)].
public record RequestingUser(Guid UserId, string Role, Guid? ClientId, Guid? TechnicianId)
{
    public bool IsStaff => Role is RoleNames.Administrador or RoleNames.Coordinador;
    public bool IsTechnician => Role is RoleNames.Tecnico;
}
