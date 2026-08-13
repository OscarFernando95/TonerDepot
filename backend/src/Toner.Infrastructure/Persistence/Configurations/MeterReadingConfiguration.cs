using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("MeterReadings");

        builder.HasOne(m => m.Asset)
            .WithMany(a => a.MeterReadings)
            .HasForeignKey(m => m.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.RegisteredByUser)
            .WithMany()
            .HasForeignKey(m => m.RegisteredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
