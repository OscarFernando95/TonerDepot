using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class TechnicianAvailabilityConfiguration : IEntityTypeConfiguration<TechnicianAvailability>
{
    public void Configure(EntityTypeBuilder<TechnicianAvailability> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("TechnicianAvailabilities");

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Reason).HasMaxLength(300);

        builder.HasOne(a => a.Technician)
            .WithMany(t => t.AvailabilityHistory)
            .HasForeignKey(a => a.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
