using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Dashboard.Dtos;
using Toner.Domain.Enums;

namespace Toner.Application.Dashboard;

// Módulo 11: métricas operativas. Las cuentas de tiempo (MTTR, SLA, mantenimiento a tiempo, utilización)
// se calculan trayendo solo las columnas necesarias y agregando en memoria, porque EF Core no traduce
// de forma confiable aritmética de TimeSpan dentro de Average/agregaciones a SQL.
public class DashboardService : IDashboardService
{
    // Metas de SLA por prioridad (medidas de CreatedAt a ResolvedAt). Decisión de negocio confirmada
    // con el usuario: valores por defecto recomendados, no derivables del modelo de datos.
    private static readonly Dictionary<ServiceTicketPriority, int> SlaTargetHours = new()
    {
        [ServiceTicketPriority.Critica] = 4,
        [ServiceTicketPriority.Alta] = 8,
        [ServiceTicketPriority.Media] = 24,
        [ServiceTicketPriority.Baja] = 72
    };

    // Ventana "a tiempo" para mantenimiento preventivo: CompletedAt <= ScheduledDate + 3 días.
    // Confirmado con el usuario (recomendado) al no existir un valor explícito en el modelo original.
    private const int MaintenanceOnTimeWindowDays = 3;

    // Base de 8h/día para expresar horas registradas como porcentaje de utilización. Es un supuesto
    // documentado (no una política de negocio confirmada) — ver Alcance en el README del módulo 11.
    private const double AssumedHoursPerDay = 8.0;

    private readonly IApplicationDbContext _db;

    public DashboardService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(int periodDays, CancellationToken cancellationToken = default)
    {
        if (periodDays <= 0)
        {
            periodDays = 30;
        }

        var periodStart = DateTime.UtcNow.AddDays(-periodDays);

        var resolvedTickets = await _db.ServiceTickets
            .Where(t => t.ResolvedAt != null && t.ResolvedAt >= periodStart)
            .Select(t => new ResolvedTicketRow(t.CreatedAt, t.ResolvedAt!.Value, t.Priority))
            .ToListAsync(cancellationToken);

        var completedOrders = await _db.MaintenanceOrders
            .Where(o => o.Status == MaintenanceOrderStatus.Completada && o.CompletedAt != null && o.CompletedAt >= periodStart)
            .Select(o => new CompletedOrderRow(o.ScheduledDate, o.CompletedAt!.Value))
            .ToListAsync(cancellationToken);

        var openTickets = await _db.ServiceTickets
            .Where(t => t.Status == ServiceTicketStatus.Abierto || t.Status == ServiceTicketStatus.SinAsignar)
            .Select(t => new OpenTicketRow(t.Status, t.ClientLocation.CityId, t.ClientLocation.City.Name))
            .ToListAsync(cancellationToken);

        var timeLogs = await _db.TimeLogs
            .Where(l => l.StartTime >= periodStart && l.EndTime != null)
            .Select(l => new TimeLogRow(l.TechnicianId, l.StartTime, l.EndTime!.Value, l.Technician.User.FullName))
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            PeriodDays = periodDays,
            Mttr = BuildMttr(resolvedTickets),
            MaintenanceCompliance = BuildMaintenanceCompliance(completedOrders),
            TicketsByCity = BuildCityBacklog(openTickets),
            TechnicianUtilization = BuildUtilization(timeLogs, periodDays),
            SlaCompliance = BuildSlaCompliance(resolvedTickets)
        };
    }

    private static MttrDto BuildMttr(IReadOnlyCollection<ResolvedTicketRow> resolved)
    {
        if (resolved.Count == 0)
        {
            return new MttrDto { AverageResolutionHours = null, ResolvedTicketCount = 0 };
        }

        var hours = resolved.Select(t => (t.ResolvedAt - t.CreatedAt).TotalHours).Average();
        return new MttrDto { AverageResolutionHours = Math.Round(hours, 1), ResolvedTicketCount = resolved.Count };
    }

    private static MaintenanceComplianceDto BuildMaintenanceCompliance(IReadOnlyCollection<CompletedOrderRow> completed)
    {
        if (completed.Count == 0)
        {
            return new MaintenanceComplianceDto
            {
                WindowDays = MaintenanceOnTimeWindowDays,
                OnTimePercentage = null,
                CompletedCount = 0,
                OnTimeCount = 0
            };
        }

        var onTime = completed.Count(o => o.CompletedAt <= o.ScheduledDate.AddDays(MaintenanceOnTimeWindowDays));
        return new MaintenanceComplianceDto
        {
            WindowDays = MaintenanceOnTimeWindowDays,
            OnTimePercentage = Math.Round(onTime * 100.0 / completed.Count, 1),
            CompletedCount = completed.Count,
            OnTimeCount = onTime
        };
    }

    private static List<CityTicketBacklogDto> BuildCityBacklog(IReadOnlyCollection<OpenTicketRow> tickets)
    {
        return tickets
            .GroupBy(t => new { t.CityId, t.CityName })
            .Select(g => new CityTicketBacklogDto
            {
                CityId = g.Key.CityId,
                CityName = g.Key.CityName,
                OpenCount = g.Count(x => x.Status == ServiceTicketStatus.Abierto),
                UnassignedCount = g.Count(x => x.Status == ServiceTicketStatus.SinAsignar)
            })
            .OrderByDescending(c => c.OpenCount + c.UnassignedCount)
            .ToList();
    }

    private static List<TechnicianUtilizationDto> BuildUtilization(IReadOnlyCollection<TimeLogRow> logs, int periodDays)
    {
        var maxHours = periodDays * AssumedHoursPerDay;

        return logs
            .GroupBy(l => new { l.TechnicianId, l.TechnicianName })
            .Select(g =>
            {
                var hours = g.Sum(x => (x.EndTime - x.StartTime).TotalHours);
                return new TechnicianUtilizationDto
                {
                    TechnicianId = g.Key.TechnicianId,
                    TechnicianName = g.Key.TechnicianName,
                    HoursLogged = Math.Round(hours, 1),
                    UtilizationPercentage = maxHours > 0 ? Math.Round(Math.Min(hours / maxHours * 100, 100), 1) : 0
                };
            })
            .OrderByDescending(t => t.HoursLogged)
            .ToList();
    }

    private static SlaComplianceDto BuildSlaCompliance(IReadOnlyCollection<ResolvedTicketRow> resolved)
    {
        var byPriority = new List<SlaPriorityComplianceDto>();

        foreach (var priority in Enum.GetValues<ServiceTicketPriority>())
        {
            var targetHours = SlaTargetHours[priority];
            var subset = resolved.Where(t => t.Priority == priority).ToList();

            if (subset.Count == 0)
            {
                byPriority.Add(new SlaPriorityComplianceDto
                {
                    Priority = priority.ToString(),
                    TargetHours = targetHours,
                    ResolvedCount = 0,
                    WithinSlaCount = 0,
                    CompliancePercentage = null
                });
                continue;
            }

            var withinSla = subset.Count(t => (t.ResolvedAt - t.CreatedAt).TotalHours <= targetHours);
            byPriority.Add(new SlaPriorityComplianceDto
            {
                Priority = priority.ToString(),
                TargetHours = targetHours,
                ResolvedCount = subset.Count,
                WithinSlaCount = withinSla,
                CompliancePercentage = Math.Round(withinSla * 100.0 / subset.Count, 1)
            });
        }

        var totalResolved = resolved.Count;
        var totalWithinSla = resolved.Count(t => (t.ResolvedAt - t.CreatedAt).TotalHours <= SlaTargetHours[t.Priority]);

        return new SlaComplianceDto
        {
            OverallCompliancePercentage = totalResolved == 0 ? null : Math.Round(totalWithinSla * 100.0 / totalResolved, 1),
            ByPriority = byPriority
        };
    }

    private sealed record ResolvedTicketRow(DateTime CreatedAt, DateTime ResolvedAt, ServiceTicketPriority Priority);

    private sealed record CompletedOrderRow(DateTime ScheduledDate, DateTime CompletedAt);

    private sealed record OpenTicketRow(ServiceTicketStatus Status, Guid CityId, string CityName);

    private sealed record TimeLogRow(Guid TechnicianId, DateTime StartTime, DateTime EndTime, string TechnicianName);
}
