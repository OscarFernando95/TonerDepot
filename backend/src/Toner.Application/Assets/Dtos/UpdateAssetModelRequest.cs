namespace Toner.Application.Assets.Dtos;

public class UpdateAssetModelRequest
{
    public string Name { get; set; } = string.Empty;

    public int GeneralPrintThreshold { get; set; }
    public int GeneralMonthsInterval { get; set; }
    public int UnitsPrintThreshold { get; set; }
    public int UnitsMonthsInterval { get; set; }
    public int ConsumablesPrintThreshold { get; set; }

    // Reemplazo completo, como el resto: omitirlo deja el modelo sin límite de alquiler.
    public int? RentalRetirementPrintThreshold { get; set; }
    public int RentalRetirementWarningPrints { get; set; } = 50000;
}
