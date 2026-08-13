namespace Toner.Application.Assets.Dtos;

public class MeterReadingDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public DateTime ReadingDate { get; set; }
    public long CounterValue { get; set; }
    public string? RegisteredByUserName { get; set; }
}
