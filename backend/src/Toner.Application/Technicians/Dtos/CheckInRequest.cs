namespace Toner.Application.Technicians.Dtos;

public class CheckInRequest
{
    // Exactamente uno de los tres debe venir.
    public Guid? ServiceTicketId { get; set; }
    public Guid? MaintenanceOrderId { get; set; }
    public Guid? AssetId { get; set; }

    // Ubicación del técnico al llegar (WGS84). Opcional: si falta se registra como "sin ubicación" y se
    // alerta, pero no se bloquea. AccuracyMeters es la precisión que reportó el GPS.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyMeters { get; set; }

    // Foto "antes" (falla o estado del equipo) ya subida por POST /technicians/me/evidence. Obligatoria
    // para tickets y órdenes (no para instalaciones, que no tienen ticket/orden al que atar la foto).
    public Guid? BeforeEvidenceId { get; set; }
}
