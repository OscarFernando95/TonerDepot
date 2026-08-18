using Microsoft.EntityFrameworkCore;
using Toner.Infrastructure.Persistence;

namespace Toner.Api.Middleware;

// Abre la conexión del TonerDbContext scoped UNA vez por request, en vez de dejar que EF Core la
// abra y cierre por cada consulta individual. Sin esto, TenantContextInterceptor.ConnectionOpenedAsync
// (que hace un round-trip de set_config por apertura de conexión para las políticas RLS) se dispara
// una vez por consulta — un endpoint con 6 consultas paga 6 round-trips solo para el contexto de
// tenencia, además del trabajo real (CODE_QUALITY_AUDIT.md hallazgo #10). Con la conexión ya abierta
// explícitamente, EF Core no la cierra entre comandos dentro del mismo DbContext, así que el
// interceptor corre una sola vez por request; se cierra sola cuando el scope de DI libera el
// DbContext al final de la request.
//
// Va DESPUÉS de UseAuthentication a propósito (mismo razonamiento que TenantContextMiddleware, que
// evalúa el contexto de forma perezosa contra HttpContext.User): si se abriera antes, la conexión
// quedaría fijada al contexto Anonymous de esa evaluación temprana para el resto de la request, en
// vez de resolver al contexto autenticado real (Staff/Cliente) una vez que UseAuthentication ya
// corrió.
//
// Se salta /health a propósito: ese endpoint abre su propia conexión de corta vida vía
// IDbContextFactory (ver PostgresHealthCheck), y un orquestador que lo sondea con frecuencia no
// debería pagar el costo de abrir además esta conexión scoped que no va a usar.
public class OpenDbConnectionMiddleware
{
    private readonly RequestDelegate _next;

    public OpenDbConnectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TonerDbContext db)
    {
        if (!context.Request.Path.StartsWithSegments("/health"))
        {
            await db.Database.OpenConnectionAsync(context.RequestAborted);
        }

        await _next(context);
    }
}
