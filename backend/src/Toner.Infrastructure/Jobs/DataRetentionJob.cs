using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Common.Interfaces;

namespace Toner.Infrastructure.Jobs;

// Job recurrente (Hangfire): purga diaria de datos operativos que crecen sin techo (AUDIT.md H-27).
//
// Hoy solo UserSessions. ExceptionLogs queda fuera a propósito: el rol de la aplicación no tiene SELECT
// sobre esa tabla (migración AddRowLevelSecurity, hallazgo #14) y un DELETE con WHERE por fecha lo
// necesita; habilitarlo es una decisión de seguridad pendiente, no algo que este job deba saltarse.
public class DataRetentionJob
{
    private readonly IApplicationDbContext _db;
    private readonly DataRetentionOptions _options;
    private readonly IExceptionLogger _exceptionLogger;
    private readonly ILogger<DataRetentionJob> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public DataRetentionJob(
        IApplicationDbContext db,
        IOptions<DataRetentionOptions> options,
        IExceptionLogger exceptionLogger,
        ILogger<DataRetentionJob> logger,
        ITenantContextAccessor tenantContextAccessor)
    {
        _db = db;
        _options = options.Value;
        _exceptionLogger = exceptionLogger;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Fuera de una request HTTP: scope de staff explícito, igual que los demás jobs.
        using var tenantScope = _tenantContextAccessor.Push(() => TenantContext.Staff);

        try
        {
            await ApplyAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            await _exceptionLogger.LogAsync(
                source: $"Jobs.{nameof(DataRetentionJob)}",
                exception: ex,
                cancellationToken: CancellationToken.None);
            throw;
        }
    }

    // Público para poder probarlo con una hora fija. Devuelve cuántas sesiones se eliminaron.
    public async Task<int> ApplyAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var cutoff = nowUtc.AddDays(-_options.UserSessionsDays);

        // Una sesión es purgable solo si YA terminó antes del corte:
        //  - revocada: RevokedAt < corte;
        //  - sin revocar: ExpiresAt y LastSeenAt < corte (el JWT venció hace más de la retención).
        // Con el corte en el pasado, una sesión viva (sin revocar y con ExpiresAt futuro) nunca cumple
        // ninguna de las dos, así que el índice de sesión única no se ve afectado.
        var deleted = await _db.UserSessions
            .Where(s =>
                (s.RevokedAt != null && s.RevokedAt < cutoff) ||
                (s.RevokedAt == null && s.ExpiresAt < cutoff && s.LastSeenAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Retención: {Deleted} sesión(es) terminadas hace más de {Days} días eliminadas.",
                deleted, _options.UserSessionsDays);
        }

        return deleted;
    }
}
