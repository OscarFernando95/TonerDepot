using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class CompanyHolidayOverrideConfiguration : IEntityTypeConfiguration<CompanyHolidayOverride>
{
    public void Configure(EntityTypeBuilder<CompanyHolidayOverride> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("CompanyHolidayOverrides");

        builder.Property(h => h.Name).IsRequired().HasMaxLength(120);
        builder.HasIndex(h => h.Date).IsUnique();
    }
}
