namespace Toner.Application.Assets.Dtos;

public class CreateMeterReadingRequest
{
    public DateTime? ReadingDate { get; set; }
    public long CounterValue { get; set; }
}
