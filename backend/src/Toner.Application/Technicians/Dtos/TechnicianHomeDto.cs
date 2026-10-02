namespace Toner.Application.Technicians.Dtos;

// Resumen del Inicio del técnico: su estado, la visita en curso, lo que sigue en su agenda y lo que conviene saber antes
// de salir (stock bajo de su zona).
public class TechnicianHomeDto
{
    public IReadOnlyList<string> ZoneNames { get; set; } = Array.Empty<string>();

    // Dentro de su horario laboral ahora mismo (día hábil, tramo vigente, sin permiso).
    public bool IsWorkingNow { get; set; }
    public DateTime? TimeOffUntil { get; set; }

    // Su jornada de hoy, p. ej. "08:00–12:00, 14:00–17:00"; null si hoy no trabaja (fin de semana, festivo).
    public string? TodayShift { get; set; }

    public int VisitsClosedToday { get; set; }
    public int MinutesWorkedToday { get; set; }

    public HomeJobDto? ActiveVisit { get; set; }
    public DateTime? ActiveVisitStartedAt { get; set; }

    // Pendiente de él, en orden de atención: lo que está en curso, luego por prioridad y antigüedad.
    public IReadOnlyList<HomeJobDto> Agenda { get; set; } = Array.Empty<HomeJobDto>();

    public IReadOnlyList<HomeLowStockDto> LowStock { get; set; } = Array.Empty<HomeLowStockDto>();
}

public class HomeJobDto
{
    // Ticket | Orden
    public string Kind { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    // Qué pasa (descripción del ticket, recortada); vacío en las órdenes.
    public string? Summary { get; set; }
    public string? ClientName { get; set; }
    public string? LocationName { get; set; }
    public string? CityName { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    // Baja | Media | Alta | Critica (las órdenes se tratan como Media)
    public string Priority { get; set; } = "Media";
    public string Status { get; set; } = string.Empty;
    public bool InProgress { get; set; }
    public DateTime Since { get; set; }
}

public class HomeLowStockDto
{
    public string ItemName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int MinimumStock { get; set; }
}
