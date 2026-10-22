namespace Toner.Application.Assets.Dtos;

public class AssetModelDto
{
    public Guid Id { get; set; }
    public Guid AssetBrandId { get; set; }
    public string Name { get; set; } = string.Empty;

    public int GeneralPrintThreshold { get; set; }
    public int GeneralMonthsInterval { get; set; }
    public int UnitsPrintThreshold { get; set; }
    public int UnitsMonthsInterval { get; set; }
    public int ConsumablesPrintThreshold { get; set; }

    // Impresiones (contador total) a las que el equipo debe salir de alquiler; null = sin límite. Y cuántas antes se avisa.
    public int? RentalRetirementPrintThreshold { get; set; }
    public int RentalRetirementWarningPrints { get; set; } = 50000;
}
