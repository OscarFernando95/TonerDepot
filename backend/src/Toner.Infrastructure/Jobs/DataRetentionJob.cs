using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Common.Interfaces;
using Toner.Application.Evidences;
using Toner.Infrastructure.Persistence;

namespace Toner.Infrastructure.Jobs;

// Job recurrente (Hangfire): purga diaria de datos operativos que crecen sin techo (AUDIT.md H-27).
//
// UserSessions, ExceptionLogs y el archivo de las fotos de evidencia (H-44). Sobre ExceptionLogs toner_app solo
// tiene SELECT de la columna CreatedAt
// (migración GrantExceptionLogsCreatedAtSelect): lo justo para el WHERE de la purga, sin poder leer el
// stack trace ni el correo (hallazgo #14).
public class DataRetentionJob
{
    private readonly TonerDbContext _db;
    private readonly DataRetentionOptions _options;
    private readonly IEvidenceStorage _evidenceStorage;
    private readonly IExceptionLogger _exceptionLogger;
    private readonly ILogger<DataRetentionJob> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public DataRetentionJob(
        TonerDbContext db,
        IOptions<DataRetentionOptions> options,
        IEvidenceStorage evidenceStorage,
        IExceptionLogger exceptionLogger,
        ILogger<DataRetentionJob> logger,
        ITenantContextAccessor tenantContextAccessor)
    {
        _db = db;
        _options = options.Value;
        _evidenceStorage = evidenceStorage;
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

        var purgedEvidence = await PurgeEvidenceContentAsync(nowUtc.AddMonths(-_options.EvidenceMonths), nowUtc, cancellationToken);

        return deleted + deletedLogs + purgedEvidence;
    }

    // Fotos de evidencia vencidas: se borra el ARCHIVO de Blob y se conserva la fila con ContentPurgedAt (AUDIT.md
    // H-44) — queda la prueba de que la visita tuvo fotos, quién las tomó y cuándo. Por lotes: la primera corrida en
    // producción puede encontrar meses de fotos. Primero se borra el blob y después se marca la fila: si algo falla
    // entre ambos pasos, la siguiente corrida repite el borrado (DeleteIfExists, inocuo) y marca.
    private async Task<int> PurgeEvidenceContentAsync(DateTime cutoff, DateTime nowUtc, CancellationToken cancellationToken)
    {
        const int batchSize = 200;
        var purged = 0;

        while (true)
        {
            var batch = await _db.Evidences
                .Where(e => e.ContentPurgedAt == null && e.UploadedAt < cutoff)
                .OrderBy(e => e.UploadedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var evidence in batch)
            {
                await _evidenceStorage.DeleteAsync(evidence.FileUrl, cancellationToken);
                evidence.ContentPurgedAt = nowUtc;
            }

            await _db.SaveChangesAsync(cancellationToken);
            purged += batch.Count;

            if (batch.Count < batchSize)
            {
                break;
            }
        }

        if (purged > 0)
        {
            _logger.LogInformation(
                "Retención: archivo de {Count} foto(s) de evidencia con más de {Months} meses eliminado de Blob (se conservan los registros).",
                purged, _options.EvidenceMonths);
        }

        return purged;
    }
}
