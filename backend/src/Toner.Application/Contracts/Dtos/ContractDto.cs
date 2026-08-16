namespace Toner.Application.Contracts.Dtos;

public class ContractDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? IncludedPrintsPerMonth { get; set; }
    public decimal? PricePerExtraPage { get; set; }
    public string? Notes { get; set; }
    public int AssetCount { get; set; }
    public List<string> CityNames { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
