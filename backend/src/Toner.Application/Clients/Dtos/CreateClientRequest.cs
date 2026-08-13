namespace Toner.Application.Clients.Dtos;

public class CreateClientRequest
{
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    // Al menos una sede es obligatoria: garantiza que todo cliente nace con dónde prestarle servicio,
    // en vez de depender de que alguien se acuerde de agregarla después.
    public List<CreateClientLocationRequest> Locations { get; set; } = new();
}
