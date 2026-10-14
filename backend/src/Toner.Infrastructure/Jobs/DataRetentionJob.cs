using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Common.Interfaces;
using Toner.Application.Evidences;
using Toner.Infrastructure.Persistence;

namespace Toner.Infrastructure.Jobs;

// Job recurrente (Hangfire): purga diaria de datos operativos que crecen sin techo (AUDIT.md H-27).
//
// UserSessions, ExceptionLogs, el archivo de las fotos de evidencia (H-44) y los archivos huérfanos (H-47). Sobre ExceptionLogs toner_app solo
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

        var evidenceCutoff = nowUtc.AddMonths(-_options.EvidenceMonths);
        var purgedEvidence = await PurgeEvidenceContentAsync(evidenceCutoff, nowUtc, cancellationToken);
        // Después de la purga de H-44: las filas vencidas ya están marcadas y sus archivos ya no cuentan como vivos.
        var orphanFiles = await DeleteOrphanEvidenceFilesAsync(evidenceCutoff, cancellationToken);

        return deleted + deletedLogs + purgedEvidence + orphanFiles;
    }

    // Archivos de evidencia sin fila viva (AUDIT.md H-47). La purga de arriba recorre FILAS, así que un archivo sin fila
    // nunca la alcanzaba y quedaba fuera de la retención: pasa si el proceso muere entre subir el archivo y el
    // SaveChanges (el catch de EvidenceService.UploadAsync cubre excepciones, no un corte del proceso) o si se borra
    // una fila a mano. Se borra el archivo más viejo que la retención que no tiene una fila sin ContentPurgedAt; uno
    // con fila viva lo deja, aunque su archivo se haya escrito justo antes del corte.
    private async Task<int> DeleteOrphanEvidenceFilesAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        // Evidences tiene RLS: fuera del contexto de staff no se ven las filas de otros clientes, y el barrido daría por
        // huérfanos archivos que sí tienen fila. Se niega ANTES de listar o borrar nada.
        if (_tenantContextAccessor.Current?.IsStaff != true)
        {
            throw new InvalidOperationException(
                "El barrido de archivos huérfanos exige el contexto de staff: con RLS, cualquier otro contexto no ve todas las filas de Evidences.");
        }

        const int batchSize = 200;
        var deleted = 0;
        var batch = new List<string>(batchSize);

        async Task FlushAsync()
        {
            var alive = await _db.Evidences
                .Where(e => batch.Contains(e.FileUrl) && e.ContentPurgedAt == null)
                .Select(e => e.FileUrl)
                .ToListAsync(cancellationToken);
            foreach (var key in batch.Except(alive))
            {
                await _evidenceStorage.DeleteAsync(key, cancellationToken);
                deleted++;
            }
            batch.Clear();
        }

        await foreach (var key in _evidenceStorage.ListKeysOlderThanAsync(cutoff, cancellationToken))
        {
            batch.Add(key);
            if (batch.Count == batchSize)
            {
                await FlushAsync();
            }
        }

        if (batch.Count > 0)
        {
            await FlushAsync();
        }

        if (deleted > 0)
        {
            // Warning: un huérfano es una anomalía (un upload cortado o una fila borrada a mano), no el flujo normal.
            _logger.LogWarning(
                "Retención: {Count} archivo(s) de evidencia sin registro vivo y con más de {Months} meses eliminados de Blob.",
                deleted, _options.EvidenceMonths);
        }

        return deleted;
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
