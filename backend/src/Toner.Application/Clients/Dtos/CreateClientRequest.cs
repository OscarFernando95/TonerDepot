namespace Toner.Application.Clients.Dtos;

public class CreateClientRequest
{
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    // true (default): cliente con contrato de alquiler. false: cliente externo — ver Client.IsContractClient.
    public bool IsContractClient { get; set; } = true;
    // "HorarioOficina" (default) | "Continuo24x7": cómo se cuenta su SLA y a quién se le puede asignar.
    public string SupportCoverage { get; set; } = "HorarioOficina";

    // Al menos una sede es obligatoria: garantiza que todo cliente nace con dónde prestarle servicio,
    // en vez de depender de que alguien se acuerde de agregarla después.
    public List<CreateClientLocationRequest> Locations { get; set; } = new();
}
