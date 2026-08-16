namespace Toner.Application.Contracts.Dtos;

public class ContractAssetDto
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid AssetId { get; set; }
    public string AssetBrandName { get; set; } = string.Empty;
    public string AssetModel { get; set; } = string.Empty;
    public string AssetSerialNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Area { get; set; }
    public long? LastMeterReading { get; set; }
    public double? AverageMonthlyPrints { get; set; }
}
