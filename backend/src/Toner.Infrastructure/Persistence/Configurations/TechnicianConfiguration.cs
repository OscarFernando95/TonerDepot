using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("Technicians");

        builder.Property(t => t.Phone).HasMaxLength(50);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        // La relación Technician.UserId -> User se configura desde UserConfiguration (dueña de la FK 1:1).
    }
}
