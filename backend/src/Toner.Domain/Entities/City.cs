using Toner.Domain.Common;

namespace Toner.Domain.Entities;

public class City : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? StateOrProvince { get; set; }

    public ICollection<ClientLocation> ClientLocations { get; set; } = new List<ClientLocation>();
    public ICollection<TechnicianCoverage> TechnicianCoverages { get; set; } = new List<TechnicianCoverage>();
}
