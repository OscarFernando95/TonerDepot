using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("Cities");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.StateOrProvince).IsRequired().HasMaxLength(150);

        // Nombres de municipio se repiten entre departamentos distintos (ej. "La Unión" existe en 4
        // departamentos) — el único real es la combinación, no el nombre solo.
        builder.HasIndex(c => new { c.Name, c.StateOrProvince }).IsUnique();
    }
}
