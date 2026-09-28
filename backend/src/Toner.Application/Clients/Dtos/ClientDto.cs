namespace Toner.Application.Clients.Dtos;

public class ClientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public bool IsContractClient { get; set; }
    // "HorarioOficina" | "Continuo24x7" (ver SupportCoverage).
    public string SupportCoverage { get; set; } = string.Empty;
    public int LocationCount { get; set; }
    public List<string> CityNames { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
