namespace Toner.Application.Assets.Dtos;

public class CreateAssetModelRequest
{
    public string Name { get; set; } = string.Empty;

    public int GeneralPrintThreshold { get; set; } = 30000;
    public int GeneralMonthsInterval { get; set; } = 6;
    public int UnitsPrintThreshold { get; set; } = 30000;
    public int UnitsMonthsInterval { get; set; } = 6;
    public int ConsumablesPrintThreshold { get; set; } = 60000;
}
