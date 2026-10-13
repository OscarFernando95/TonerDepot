using System.Runtime.CompilerServices;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Jobs;

// Aviso inmediato al despachador del outbox tras un SaveChanges que insertó correos pendientes, para que la
// contraseña llegue en segundos como antes (AUDIT.md H-19). Es best-effort: si encolar el aviso falla, solo se
// loguea — el job recurrente de cada minuto es la garantía. Mismo patrón que RealtimeChangeInterceptor.
public sealed class PasswordEmailOutboxKickInterceptor : SaveChangesInterceptor
{
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<PasswordEmailOutboxKickInterceptor> _logger;
    private readonly ConditionalWeakTable<DbContext, object> _pending = new();

    public PasswordEmailOutboxKickInterceptor(IBackgroundJobClient jobs, ILogger<PasswordEmailOutboxKickInterceptor> logger)
    {
        _jobs = jobs;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Kick(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Kick(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        var inserted = context.ChangeTracker.Entries<PasswordEmailOutbox>().Any(e => e.State == EntityState.Added);
        if (inserted)
        {
            _pending.AddOrUpdate(context, new object());
        }
    }

    private void Kick(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out _)) return;
        _pending.Remove(context);

        try
        {
            _jobs.Enqueue<PasswordEmailOutboxDispatcher>(d => d.RunAsync(CancellationToken.None));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo avisar al despachador del outbox; lo recogerá el job recurrente.");
        }
    }

    private void Discard(DbContext? context)
    {
        if (context is not null) _pending.Remove(context);
    }
}
