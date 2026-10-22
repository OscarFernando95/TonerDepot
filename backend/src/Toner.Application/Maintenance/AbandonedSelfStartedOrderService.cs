using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Enums;

namespace Toner.Application.Maintenance;

// Cancela las órdenes que el técnico inició por su cuenta y nunca empezó. Sin esto, una orden abandonada quedaría
// abierta para siempre y el motor del cronograma se saltaría ese equipo (solo genera orden si no hay una abierta).
// Lo corre AbandonedSelfStartedOrderJob. Al pasar a Cancelada, el interceptor de push avisa al técnico.
public class AbandonedSelfStartedOrderService
{
    private const int BatchSize = 200;

    private readonly IApplicationDbContext _db;
    private readonly AbandonedSelfStartedOrderOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<AbandonedSelfStartedOrderService> _logger;

    public AbandonedSelfStartedOrderService(
        IApplicationDbContext db,
        IOptions<AbandonedSelfStartedOrderOptions> options,
        TimeProvider time,
        ILogger<AbandonedSelfStartedOrderService> logger)
    {
        _db = db;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    // Devuelve cuántas canceló. Un único SaveChanges por tanda.
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _time.GetUtcNow().UtcDateTime.AddHours(-Math.Max(1, _options.ExpireAfterHours));

        // Asignada = nunca hizo check-in (el check-in la pasa a EnProceso y no vuelve atrás). Y solo cuenta si TODA su
        // historia es la del propio técnico: si otro técnico la tomó o el staff la reasignó, ya no es una orden
        // abandonada por quien la inició y se la deja a quien la tiene.
        var orders = await _db.MaintenanceOrders
            .Where(o => o.Status == MaintenanceOrderStatus.Asignada
                && o.IsManual
                && o.AssignmentHistories.All(h => h.AssignmentType == AssignmentType.IniciadaPorTecnico)
                && o.AssignmentHistories.Any(h => h.AssignedAt < cutoff))
            .OrderBy(o => o.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var order in orders)
        {
            order.Status = MaintenanceOrderStatus.Cancelada;
            _logger.LogInformation(
                "Orden autoiniciada caducada sin check-in: orden {OrderId}, técnico {TechnicianId}, activo {AssetId}",
                order.Id, order.TechnicianId, order.AssetId);
        }

        if (orders.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return orders.Count;
    }
}
