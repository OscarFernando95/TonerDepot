namespace Toner.Application.ClientPortal.Dtos;

// Resumen del inicio del cliente: lo que necesita saber de un vistazo y a dónde ir. Solo servicio — nunca contratos, límites
// de alquiler ni nada interno. Respeta el alcance del usuario (su empresa y, si las tiene restringidas, sus sedes).
public class ClientHomeDto
{
    public string ClientName { get; set; } = string.Empty;

    // Cuántas sedes puede ver (para mostrar el selector/etiqueta solo cuando hay más de una).
    public int LocationCount { get; set; }

    // Tickets sin cerrar (Abierto, SinAsignar, Asignado, EnProceso) y, de ellos, los que ya tienen técnico trabajando.
    public int OpenTickets { get; set; }
    public int TicketsInProgress { get; set; }

    // Mantenimientos abiertos (Pendiente, Asignada, EnProceso): cuántos tienen ya visita programada por el técnico, cuántos
    // están en curso ahora y cuántos aún no tienen fecha.
    public int OpenMaintenance { get; set; }
    public int MaintenanceScheduled { get; set; }
    public int MaintenanceInProgress { get; set; }
    public int MaintenanceWithoutVisitDate { get; set; }

    // Instalaciones pendientes de sus sedes y, de ellas, las que un técnico tiene en curso ahora.
    public int PendingInstallations { get; set; }
    public int InstallationsInProgress { get; set; }

    // Estado de sus máquinas: instaladas, las que están en mantenimiento, y cuántas tienen un ticket abierto ahora.
    public int InstalledAssets { get; set; }
    public int AssetsInMaintenance { get; set; }
    public int AssetsWithOpenTicket { get; set; }

    // Los más recientes; el resto se ve en sus listas.
    public IReadOnlyList<ClientHomeTicketDto> RecentTickets { get; set; } = Array.Empty<ClientHomeTicketDto>();
    public IReadOnlyList<ClientHomeMaintenanceDto> UpcomingMaintenance { get; set; } = Array.Empty<ClientHomeMaintenanceDto>();
    public IReadOnlyList<ClientHomeInstallationDto> Installations { get; set; } = Array.Empty<ClientHomeInstallationDto>();
}

public class ClientHomeTicketDto
{
    public Guid Id { get; set; }
    public string Equipment { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? TechnicianName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientHomeMaintenanceDto
{
    public Guid Id { get; set; }
    public string Equipment { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    // La visita que fijó el técnico (UTC), si ya la programó.
    public DateTime? PlannedAt { get; set; }
    public string? TechnicianName { get; set; }
    public int CommentCount { get; set; }
}

public class ClientHomeInstallationDto
{
    public Guid AssetId { get; set; }
    public string Equipment { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    // Un técnico está instalándola ahora.
    public bool InProgress { get; set; }
}
