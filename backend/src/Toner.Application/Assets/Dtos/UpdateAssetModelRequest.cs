namespace Toner.Application.Assets.Dtos;

public class UpdateAssetModelRequest
{
    public string Name { get; set; } = string.Empty;

    public int GeneralPrintThreshold { get; set; }
    public int GeneralMonthsInterval { get; set; }
    public int UnitsPrintThreshold { get; set; }
    public int UnitsMonthsInterval { get; set; }
    public int ConsumablesPrintThreshold { get; set; }
}
