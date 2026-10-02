using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("Zones");

        builder.Property(z => z.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(z => z.Name).IsUnique();

        // Borrar una zona libera sus municipios (quedan sin zona); ZoneService impide borrarla si tiene técnicos.
        builder.HasMany(z => z.Cities)
            .WithOne(c => c.Zone)
            .HasForeignKey(c => c.ZoneId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class TechnicianZoneConfiguration : IEntityTypeConfiguration<TechnicianZone>
{
    public void Configure(EntityTypeBuilder<TechnicianZone> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("TechnicianZones");

        builder.HasIndex(tz => new { tz.TechnicianId, tz.ZoneId }).IsUnique();
        builder.HasIndex(tz => tz.ZoneId);

        builder.HasOne(tz => tz.Technician)
            .WithMany(t => t.TechnicianZones)
            .HasForeignKey(tz => tz.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tz => tz.Zone)
            .WithMany(z => z.TechnicianZones)
            .HasForeignKey(tz => tz.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
