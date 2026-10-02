namespace Toner.Application.Inventory.Analytics;

public class TonerFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Guid? ZoneId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? ModelId { get; set; }
}

public class TonerMachineRowDto
{
    public Guid AssetId { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? LocationName { get; set; }
    public string? ZoneName { get; set; }

    // Tóner usado en el rango, separado por cómo se entregó.
    public int TotalUnits { get; set; }
    public int ChangedByTechnicianUnits { get; set; }
    public int DeliveredToUserUnits { get; set; }

    // Tóner cuya duración quedó medida en el rango (un cambio posterior cerró el intervalo) y páginas por tóner.
    public int MeasuredUnits { get; set; }
    public double? AvgPagesPerUnit { get; set; }

    // Páginas que recorrió el contador entre los registros de tóner del rango.
    public long? PagesInRange { get; set; }

    // Duración real contra lo que rinde ese mismo tóner en el promedio de otras máquinas del modelo (100 = igual).
    public double? VsModelPercent { get; set; }

    public DateTime? LastEventAt { get; set; }
    public long? LastCounter { get; set; }
}

public class TonerGroupRowDto
{
    public string Name { get; set; } = string.Empty;
    public int Machines { get; set; }
    public int TotalUnits { get; set; }
    public double? AvgPagesPerUnit { get; set; }
}

public class TonerMonthDto
{
    // yyyy-MM
    public string Month { get; set; } = string.Empty;
    public int Units { get; set; }
}

public class TonerSummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalUnits { get; set; }
    public int ChangedByTechnicianUnits { get; set; }
    public int DeliveredToUserUnits { get; set; }
    public int Machines { get; set; }
    public int MeasuredUnits { get; set; }
    public double? AvgPagesPerUnit { get; set; }
    public IReadOnlyList<TonerMonthDto> Monthly { get; set; } = Array.Empty<TonerMonthDto>();
    public IReadOnlyList<TonerGroupRowDto> ByClient { get; set; } = Array.Empty<TonerGroupRowDto>();
    public IReadOnlyList<TonerGroupRowDto> ByZone { get; set; } = Array.Empty<TonerGroupRowDto>();
    public IReadOnlyList<TonerGroupRowDto> ByModel { get; set; } = Array.Empty<TonerGroupRowDto>();
}
