using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Common.Interfaces;
using Toner.Infrastructure.Persistence;

namespace Toner.Infrastructure.Jobs;

// Job recurrente (Hangfire): purga diaria de datos operativos que crecen sin techo (AUDIT.md H-27).
//
// UserSessions y ExceptionLogs. Sobre ExceptionLogs toner_app solo tiene SELECT de la columna CreatedAt
// (migración GrantExceptionLogsCreatedAtSelect): lo justo para el WHERE de la purga, sin poder leer el
// stack trace ni el correo (hallazgo #14).
public class DataRetentionJob
{
    private readonly TonerDbContext _db;
    private readonly DataRetentionOptions _options;
    private readonly IExceptionLogger _exceptionLogger;
    private readonly ILogger<DataRetentionJob> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public DataRetentionJob(
        TonerDbContext db,
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

    // Público para poder probarlo con una hora fija. Devuelve cuántas filas se eliminaron en total.
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

        var exceptionCutoff = nowUtc.AddDays(-_options.ExceptionLogsDays);
        var deletedLogs = await _db.ExceptionLogs
            .Where(l => l.CreatedAt < exceptionCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedLogs > 0)
        {
            _logger.LogInformation(
                "Retención: {Deleted} registro(s) de ExceptionLogs con más de {Days} días eliminados.",
                deletedLogs, _options.ExceptionLogsDays);
        }

        return deleted + deletedLogs;
    }
}
