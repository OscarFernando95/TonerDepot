using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class TechnicianWorkIntervalConfiguration : IEntityTypeConfiguration<TechnicianWorkInterval>
{
    public void Configure(EntityTypeBuilder<TechnicianWorkInterval> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("TechnicianWorkIntervals");

        builder.HasOne(i => i.Technician)
            .WithMany(t => t.WorkIntervals)
            .HasForeignKey(i => i.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.TechnicianId, i.Day });
    }
}
