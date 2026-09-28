namespace Toner.Application.Clients.Dtos;

public class UpdateClientRequest
{
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsContractClient { get; set; } = true;
    // "HorarioOficina" (default) | "Continuo24x7": cómo se cuenta su SLA y a quién se le puede asignar.
    public string SupportCoverage { get; set; } = "HorarioOficina";
}
