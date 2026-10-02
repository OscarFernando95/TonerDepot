namespace Toner.Application.Assets.Dtos;

// Fila del módulo "Lectura de contadores" — abierto a los 5 roles, con alcance distinto por rol
// (ver AssetService.ListForMeterReadingAsync).
public class MeterReadingAssetDto
{
    public Guid AssetId { get; set; }
    public string AssetBrandName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;

    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientLocationName { get; set; }
    public string? Area { get; set; }
    public string? CityName { get; set; }

    public long? LastMeterReading { get; set; }

    // Último tóner entregado o cambiado en la máquina (con su contador) y cuántas unidades en los últimos 90 días.
    public DateTime? LastTonerAt { get; set; }
    public long? LastTonerCounter { get; set; }
    public int TonerUnitsLast90Days { get; set; }
}
