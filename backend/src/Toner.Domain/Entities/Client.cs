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

    public ICollection<ClientLocation> Locations { get; set; } = new List<ClientLocation>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
