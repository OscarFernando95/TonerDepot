using System.Security.Claims;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Common;

namespace Toner.Api.Middleware;

// Establece el TenantContext de la request, que TenantContextInterceptor propaga a Postgres como
// variables de sesión para las políticas RLS (SECURITY_AUDIT.md hallazgo #5).
//
// Va ANTES de UseAuthentication a propósito: la validación del token (SecurityStampValidator)
// consulta la tabla Users, y esa consulta también abre una conexión — sin un contexto ya
// establecido, el interceptor lanzaría. Como el contexto se evalúa de forma perezosa contra
// HttpContext.User, esa consulta temprana ve Anonymous (correcto: Users está fuera de RLS,
// justamente porque el login tiene que poder consultarla antes de que exista un usuario) y el
// controller posterior ya ve el contexto autenticado real.
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenantContextAccessor)
    {
        using var _ = tenantContextAccessor.Push(() => FromPrincipal(context.User));
        await _next(context);
    }

    private static TenantContext FromPrincipal(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return TenantContext.Anonymous;
        }

        var role = user.FindFirstValue(ClaimTypes.Role);

        // Administrador/Coordinador/Tecnico son confiables a nivel de BD: ven datos de varios
        // clientes por diseño. Su filtrado fino sigue en la capa de aplicación, sin cambios.
        if (role is RoleNames.Administrador or RoleNames.Coordinador or RoleNames.Tecnico)
        {
            return TenantContext.Staff;
        }

        if (role == RoleNames.Cliente
            && Guid.TryParse(user.FindFirstValue("client_id"), out var clientId))
        {
            return TenantContext.ForClient(clientId);
        }

        // Cualquier otro caso (rol Ventas, que hoy no tiene endpoints; o un Cliente sin client_id
        // válido) cae en Anonymous: las políticas RLS no dejan pasar ninguna fila. Fail-closed.
        return TenantContext.Anonymous;
    }
}
