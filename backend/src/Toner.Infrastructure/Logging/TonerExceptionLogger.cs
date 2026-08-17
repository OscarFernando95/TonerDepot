using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Infrastructure.Persistence;

namespace Toner.Infrastructure.Logging;

// Usa un DbContext propio y de vida corta (via IDbContextFactory) en vez del IApplicationDbContext
// scoped de la request/job: si la excepción que estamos registrando vino justamente de ese DbContext
// (ej. un DbUpdateException durante SaveChanges), su ChangeTracker puede seguir en un estado inválido;
// reutilizarlo arriesgaría que también falle el registro del error. Un contexto nuevo aísla el log
// del problema que está documentando.
public class TonerExceptionLogger : IExceptionLogger
{
    private readonly IDbContextFactory<TonerDbContext> _dbContextFactory;
    private readonly ILogger<TonerExceptionLogger> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TonerExceptionLogger(
        IDbContextFactory<TonerDbContext> dbContextFactory,
        ILogger<TonerExceptionLogger> logger,
        ITenantContextAccessor tenantContextAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task LogAsync(
        string source,
        Exception exception,
        string? requestMethod = null,
        string? requestPath = null,
        int? statusCode = null,
        Guid? userId = null,
        string? userEmail = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Scope de staff explícito: este DbContext es propio (vía IDbContextFactory) y puede
            // invocarse desde un job de Hangfire, donde no hay contexto de request. Sin esto, el
            // TenantContextInterceptor lanzaría y enmascararía la excepción original que estamos
            // intentando registrar. Ver SECURITY_AUDIT.md hallazgo #5.
            using var tenantScope = _tenantContextAccessor.Push(() => TenantContext.Staff);

            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            db.ExceptionLogs.Add(new ExceptionLog
            {
                Source = source,
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                Message = exception.Message,
                StackTrace = exception.ToString(),
                RequestMethod = requestMethod,
                RequestPath = requestPath,
                StatusCode = statusCode,
                UserId = userId,
                UserEmail = userEmail
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception loggingException)
        {
            // Si ni siquiera se puede escribir el log (ej. Postgres inalcanzable), no debe tumbar la
            // request ni ocultar la excepción original — solo queda constancia en el logger de consola.
            _logger.LogError(
                loggingException,
                "No se pudo persistir el ExceptionLog para una excepción de tipo {ExceptionType}: {OriginalMessage}",
                exception.GetType().Name,
                exception.Message);
        }
    }
}
