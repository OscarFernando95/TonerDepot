using Toner.Domain.Common;

namespace Toner.Domain.Entities;

public class Client : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; } = true;

    // true: cliente con contrato de alquiler — sus tickets se ligan a un Asset real (con hoja de vida).
    // false: cliente externo que solo pide servicio sobre equipos propios, a veces ni catalogados — el
    // ticket puede quedar sin Asset y el técnico registra marca/modelo/contador de forma opcional.
    public bool IsContractClient { get; set; } = true;

    public ICollection<ClientLocation> Locations { get; set; } = new List<ClientLocation>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
