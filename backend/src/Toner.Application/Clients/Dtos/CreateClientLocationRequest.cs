namespace Toner.Application.Clients.Dtos;

public class CreateClientLocationRequest
{
    public Guid CityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
}
