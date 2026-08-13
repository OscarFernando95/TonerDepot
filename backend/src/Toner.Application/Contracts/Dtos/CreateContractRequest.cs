namespace Toner.Application.Contracts.Dtos;

public class CreateContractRequest
{
    public Guid ClientId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? IncludedPrintsPerMonth { get; set; }
    public decimal? PricePerExtraPage { get; set; }
    public string? Notes { get; set; }
}
