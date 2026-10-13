using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Toner.Application.Common.Interfaces;
using Toner.Infrastructure.Persistence;

namespace Toner.Infrastructure.Jobs;

// Job recurrente (y avisado al momento por PasswordEmailOutboxKickInterceptor): pasa los correos pendientes del
// outbox a Hangfire (AUDIT.md H-19).
//
// Encola el job EXISTENTE SendGeneratedPasswordEmailJob, con su firma intacta (zona crítica 5): los jobs que ya
// estuvieran en cola al desplegar y los nuevos siguen el mismo camino, con la misma política de reintentos.
//
// Primero encola y DESPUÉS borra la fila: si el proceso muere entre ambos pasos, el correo se envía dos veces
// (at-least-once) — preferible a perderlo. DisableConcurrentExecution evita que el recurrente y un aviso
// inmediato encolen la misma fila a la vez.
[DisableConcurrentExecution(timeoutInSeconds: 60)]
[AutomaticRetry(Attempts = 0)] // El recurrente de cada minuto ya es el reintento.
public class PasswordEmailOutboxDispatcher
{
    private const int BatchSize = 50;

    private readonly TonerDbContext _db;
    private readonly IBackgroundJobClient _jobs;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ILogger<PasswordEmailOutboxDispatcher> _logger;

    public PasswordEmailOutboxDispatcher(
        TonerDbContext db,
        IBackgroundJobClient jobs,
        ITenantContextAccessor tenantContextAccessor,
        ILogger<PasswordEmailOutboxDispatcher> logger)
    {
        _db = db;
        _jobs = jobs;
        _tenantContextAccessor = tenantContextAccessor;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // Fuera de una request HTTP: scope de staff explícito, igual que los demás jobs.
        using var tenantScope = _tenantContextAccessor.Push(() => TenantContext.Staff);

        var pending = await _db.PasswordEmailOutbox
            .OrderBy(o => o.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var dispatched = 0;
        foreach (var row in pending)
        {
            try
            {
                _jobs.Enqueue<SendGeneratedPasswordEmailJob>(
                    job => job.RunAsync(row.Cedula, row.EncryptedPassword, CancellationToken.None));
            }
            catch (Exception ex)
            {
                // La fila se queda: la próxima ejecución lo reintenta. Nunca se loguea el ciphertext.
                _logger.LogWarning(ex,
                    "No se pudo encolar el correo de contraseña temporal para la cédula {Cedula}; se reintentará.", row.Cedula);
                break;
            }

            // Un SaveChanges por fila: un fallo a mitad de lote no duplica ni pierde las demás.
            _db.PasswordEmailOutbox.Remove(row);
            await _db.SaveChangesAsync(cancellationToken);
            dispatched++;
        }

        return dispatched;
    }
}
