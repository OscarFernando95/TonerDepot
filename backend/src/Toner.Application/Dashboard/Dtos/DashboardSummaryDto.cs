namespace Toner.Application.Dashboard.Dtos;

// Respuesta compuesta del módulo 11: una sola llamada arma las 5 tarjetas del dashboard.
// Los DTOs internos solo se usan aquí, por eso viven juntos en este archivo.
public class DashboardSummaryDto
{
    public int PeriodDays { get; set; }
    public MttrDto Mttr { get; set; } = null!;
    public MaintenanceComplianceDto MaintenanceCompliance { get; set; } = null!;
    public IReadOnlyList<CityTicketBacklogDto> TicketsByCity { get; set; } = new List<CityTicketBacklogDto>();
    public IReadOnlyList<TechnicianUtilizationDto> TechnicianUtilization { get; set; } = new List<TechnicianUtilizationDto>();
    public SlaComplianceDto SlaCompliance { get; set; } = null!;
}

public class MttrDto
{
    public double? AverageResolutionHours { get; set; }
    public int ResolvedTicketCount { get; set; }
}

public class MaintenanceComplianceDto
{
    public int WindowDays { get; set; }
    public double? OnTimePercentage { get; set; }
    public int CompletedCount { get; set; }
    public int OnTimeCount { get; set; }
}

public class CityTicketBacklogDto
{
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public int OpenCount { get; set; }
    public int UnassignedCount { get; set; }
}

public class TechnicianUtilizationDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public double HoursLogged { get; set; }
    public double UtilizationPercentage { get; set; }
}

public class SlaComplianceDto
{
    public double? OverallCompliancePercentage { get; set; }
    public IReadOnlyList<SlaPriorityComplianceDto> ByPriority { get; set; } = new List<SlaPriorityComplianceDto>();
}

public class SlaPriorityComplianceDto
{
    public string Priority { get; set; } = string.Empty;
    public int TargetHours { get; set; }
    public int ResolvedCount { get; set; }
    public int WithinSlaCount { get; set; }
    public double? CompliancePercentage { get; set; }
}
