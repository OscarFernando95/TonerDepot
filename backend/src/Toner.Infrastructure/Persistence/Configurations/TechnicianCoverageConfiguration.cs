using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class TechnicianCoverageConfiguration : IEntityTypeConfiguration<TechnicianCoverage>
{
    public void Configure(EntityTypeBuilder<TechnicianCoverage> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("TechnicianCoverages");

        builder.HasIndex(tc => new { tc.TechnicianId, tc.CityId }).IsUnique();

        builder.HasOne(tc => tc.Technician)
            .WithMany(t => t.Coverages)
            .HasForeignKey(tc => tc.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tc => tc.City)
            .WithMany(c => c.TechnicianCoverages)
            .HasForeignKey(tc => tc.CityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
