using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Toner.Application.Common.Caching;
using Toner.Application.Calendar;
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

    private readonly IApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IWorkCalendarService _calendar;

    public DashboardService(IApplicationDbContext db, IMemoryCache cache, ITenantContextAccessor tenantContextAccessor, IWorkCalendarService calendar)
    {
        _calendar = calendar;
        _db = db;
        _cache = cache;
        _tenantContextAccessor = tenantContextAccessor;
    }

    // Es la consulta más cara del sistema: cuatro barridos sin índice de soporte más agregación en
    // memoria, y se dispara en cada clic del selector de período (CODE_QUALITY_AUDIT.md hallazgo #10).
    //
    // ⚠️ A diferencia de los catálogos, esto SÍ toca tablas con RLS: ServiceTickets tiene políticas, y
    // la proyección de tickets abiertos navega a ClientLocation, que también. Que hoy sea seguro
    // cachearlo depende de que el endpoint sea solo-staff ([Authorize(Roles = StaffRoles)] en
    // DashboardController), no del modelo de datos. Por eso el tenant va EN LA CLAVE: si mañana se
    // abriera al rol Cliente, cada cliente tendría su propia entrada en vez de leer la del vecino.
    public async Task<DashboardSummaryDto> GetSummaryAsync(int periodDays, CancellationToken cancellationToken = default)
    {
        if (periodDays <= 0)
        {
            periodDays = 30;
        }

        // La clave usa el periodDays YA normalizado: si no, ?periodDays=0, =-5 y =30 crearían tres
        // entradas con exactamente el mismo contenido.
        var tenant = _tenantContextAccessor.Current;
        var cacheKey = CacheKeys.DashboardSummary(tenant?.IsStaff ?? false, tenant?.ClientId, periodDays);

        if (_cache.TryGetValue(cacheKey, out DashboardSummaryDto? cached) && cached is not null)
        {
            return cached;
        }

        var periodStart = DateTime.UtcNow.AddDays(-periodDays);

        var resolvedTickets = await _db.ServiceTickets
            .Where(t => t.ResolvedAt != null && t.ResolvedAt >= periodStart)
            .Select(t => new ResolvedTicketRow(t.CreatedAt, t.ResolvedAt!.Value, t.Priority, t.TechnicianId, t.ClientLocation.Client.SupportCoverage))
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

        // SLA y utilización se miden en horas hábiles (horario del técnico, festivos de Colombia y sus
        // períodos fuera de la oficina); un cliente 24/7 cuenta horas corridas. MTTR se deja en horas
        // corridas a propósito: mide cuánto tarda en resolverse un ticket, no el cumplimiento de un contrato.
        var now = DateTime.UtcNow;
        var calendarFrom = resolvedTickets.Count > 0 ? new[] { periodStart, resolvedTickets.Min(t => t.CreatedAt) }.Min() : periodStart;
        var technicianIds = resolvedTickets.Where(t => t.TechnicianId.HasValue).Select(t => t.TechnicianId!.Value)
            .Concat(timeLogs.Select(l => l.TechnicianId));
        var calendar = await _calendar.LoadAsync(technicianIds, calendarFrom, now, cancellationToken);

        var summary = new DashboardSummaryDto
        {
            PeriodDays = periodDays,
            Mttr = BuildMttr(resolvedTickets),
            MaintenanceCompliance = BuildMaintenanceCompliance(completedOrders),
            TicketsByCity = BuildCityBacklog(openTickets),
            TechnicianUtilization = BuildUtilization(timeLogs, periodStart, now, calendar),
            SlaCompliance = BuildSlaCompliance(resolvedTickets, calendar)
        };

        // Absoluta: 30 segundos es el techo real de antigüedad. Con expiración deslizante, un
        // dashboard abierto y refrescándose nunca volvería a consultar la base.
        _cache.Set(cacheKey, summary, CacheDurations.DashboardSummary);

        return summary;
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

    private static List<TechnicianUtilizationDto> BuildUtilization(
        IReadOnlyCollection<TimeLogRow> logs, DateTime periodStart, DateTime periodEnd, WorkCalendarContext calendar)
    {
        return logs
            .GroupBy(l => new { l.TechnicianId, l.TechnicianName })
            .Select(g =>
            {
                var hours = g.Sum(x => (x.EndTime - x.StartTime).TotalHours);
                // Denominador: horas laborales reales del técnico en el período (no un 8 h/día supuesto).
                var maxHours = calendar.BusinessHours(g.Key.TechnicianId, SupportCoverage.HorarioOficina, periodStart, periodEnd);
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

    private static SlaComplianceDto BuildSlaCompliance(IReadOnlyCollection<ResolvedTicketRow> resolved, WorkCalendarContext calendar)
    {
        double SlaHours(ResolvedTicketRow t) => calendar.BusinessHours(t.TechnicianId, t.Coverage, t.CreatedAt, t.ResolvedAt);

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

            var withinSla = subset.Count(t => SlaHours(t) <= targetHours);
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
        var totalWithinSla = resolved.Count(t => SlaHours(t) <= SlaTargetHours[t.Priority]);

        return new SlaComplianceDto
        {
            OverallCompliancePercentage = totalResolved == 0 ? null : Math.Round(totalWithinSla * 100.0 / totalResolved, 1),
            ByPriority = byPriority
        };
    }

    private sealed record ResolvedTicketRow(DateTime CreatedAt, DateTime ResolvedAt, ServiceTicketPriority Priority, Guid? TechnicianId, SupportCoverage Coverage);

    private sealed record CompletedOrderRow(DateTime ScheduledDate, DateTime CompletedAt);

    private sealed record OpenTicketRow(ServiceTicketStatus Status, Guid CityId, string CityName);

    private sealed record TimeLogRow(Guid TechnicianId, DateTime StartTime, DateTime EndTime, string TechnicianName);
}
