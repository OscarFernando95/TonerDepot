using System.Net;
using System.Security.Claims;
using FluentValidation;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;

namespace Toner.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // IExceptionLogger es scoped (crea un DbContext por log), así que se resuelve por request como
    // parámetro de InvokeAsync — no por constructor. UseMiddleware<T> construye la instancia una sola
    // vez contra el proveedor raíz de la app, así que un servicio scoped en el constructor rompería la
    // validación de scopes (o, sin validación, sería el mismo scope "atrapado" para toda la vida del proceso).
    public async Task InvokeAsync(HttpContext context, IExceptionLogger exceptionLogger)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, title, errors) = Map(ex);

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                _logger.LogError(ex, "Error no controlado procesando {Method} {Path}", context.Request.Method, context.Request.Path);

                // CancellationToken.None a propósito: si el cliente cancela la request, igual
                // queremos que el intento de log a base de datos se complete (best-effort).
                await exceptionLogger.LogAsync(
                    source: "Api",
                    exception: ex,
                    requestMethod: context.Request.Method,
                    requestPath: context.Request.Path,
                    statusCode: (int)statusCode,
                    userId: TryGetUserId(context.User),
                    userEmail: context.User.FindFirstValue(ClaimTypes.Email),
                    cancellationToken: CancellationToken.None);
            }
            else if (ex is ForbiddenException)
            {
                // Señal de mayor valor forense en un sistema multi-cliente: alguien autenticado
                // intentando acceder a datos fuera de su alcance. A diferencia de los 500, esto no se
                // persiste en ExceptionLogs (ver hallazgo #14 de SECURITY_AUDIT.md, fuera de alcance
                // aquí) — solo queda en el logger estructurado.
                _logger.LogWarning(
                    "Acceso denegado: usuario {UserId} (rol {UserRole}) intentó {Method} {Path} desde IP {IpAddress}. Motivo: {Reason}",
                    TryGetUserId(context.User),
                    context.User.FindFirstValue(ClaimTypes.Role),
                    context.Request.Method,
                    context.Request.Path,
                    context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                    ex.Message);
            }

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsJsonAsync(new
            {
                title,
                status = (int)statusCode,
                errors
            });
        }
    }

    private static (HttpStatusCode StatusCode, string Title, object? Errors) Map(Exception ex) => ex switch
    {
        ValidationException vex => (
            HttpStatusCode.BadRequest,
            "Uno o más campos no son válidos.",
            vex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),

        NotFoundException => (HttpStatusCode.NotFound, ex.Message, null),
        ConflictException => (HttpStatusCode.Conflict, ex.Message, null),
        InvalidCredentialsException => (HttpStatusCode.Unauthorized, ex.Message, null),
        ForbiddenException => (HttpStatusCode.Forbidden, ex.Message, null),

        _ => (HttpStatusCode.InternalServerError, "Ocurrió un error inesperado.", null)
    };

    private static Guid? TryGetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
