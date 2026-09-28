using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class TechnicianTimeOffConfiguration : IEntityTypeConfiguration<TechnicianTimeOff>
{
    public void Configure(EntityTypeBuilder<TechnicianTimeOff> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("TechnicianTimeOffs");

        builder.Property(t => t.Reason).HasMaxLength(300);

        builder.HasOne(t => t.Technician)
            .WithMany(x => x.TimeOffs)
            .HasForeignKey(t => t.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.TechnicianId, t.StartsAt });
    }
}
