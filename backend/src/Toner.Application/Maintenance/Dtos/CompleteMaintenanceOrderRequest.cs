namespace Toner.Application.Maintenance.Dtos;

public class CompleteMaintenanceOrderRequest
{
    public long CounterValue { get; set; }
    public DateTime? ReadingDate { get; set; }
}
