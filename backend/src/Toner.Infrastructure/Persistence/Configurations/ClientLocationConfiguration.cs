using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class ClientLocationConfiguration : IEntityTypeConfiguration<ClientLocation>
{
    public void Configure(EntityTypeBuilder<ClientLocation> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("ClientLocations");

        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Address).IsRequired().HasMaxLength(300);
        builder.Property(l => l.ContactName).HasMaxLength(200);
        builder.Property(l => l.ContactPhone).HasMaxLength(50);

        builder.HasOne(l => l.Client)
            .WithMany(c => c.Locations)
            .HasForeignKey(l => l.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.City)
            .WithMany(c => c.ClientLocations)
            .HasForeignKey(l => l.CityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
