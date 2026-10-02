namespace Toner.Application.Technicians.Dtos;

public class TechnicianSelfStatusDto
{
    public Guid TechnicianId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ActiveServiceTicketId { get; set; }
    public Guid? ActiveMaintenanceOrderId { get; set; }
    public Guid? ActiveAssetInstallationId { get; set; }
    public DateTime? CheckedInAt { get; set; }

    // Avisos del último check-out (p. ej. una pieza que dejó el stock de la zona en negativo). La visita se cerró igual.
    public IReadOnlyList<string> StockWarnings { get; set; } = Array.Empty<string>();
}
