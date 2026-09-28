namespace Toner.Application.Technicians.Dtos;

public class TimeLogDto
{
    public Guid Id { get; set; }
    public Guid? ServiceTicketId { get; set; }
    public Guid? MaintenanceOrderId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Notes { get; set; }

    // Ubicación al llegar / al cerrar frente a la sede. Estado: EnSitio | FueraDeSitio | SinUbicacion |
    // SedeSinCoordenadas. Distancia en metros (null si no se pudo calcular).
    public string? CheckInLocationStatus { get; set; }
    public double? CheckInDistanceMeters { get; set; }
    public string? CheckOutLocationStatus { get; set; }
    public double? CheckOutDistanceMeters { get; set; }
}
