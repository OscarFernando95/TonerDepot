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
        builder.Property(c => c.StateOrProvince).HasMaxLength(150);
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
