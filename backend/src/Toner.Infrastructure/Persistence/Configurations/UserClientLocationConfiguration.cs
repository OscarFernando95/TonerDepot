using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class UserClientLocationConfiguration : IEntityTypeConfiguration<UserClientLocation>
{
    public void Configure(EntityTypeBuilder<UserClientLocation> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("UserClientLocations");

        builder.HasIndex(l => new { l.UserId, l.ClientLocationId }).IsUnique();
        // Índice del predicado de la política RLS (sargable).
        builder.HasIndex(l => l.ClientId);

        builder.HasOne(l => l.User)
            .WithMany(u => u.AllowedLocations)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.ClientLocation)
            .WithMany()
            .HasForeignKey(l => l.ClientLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
