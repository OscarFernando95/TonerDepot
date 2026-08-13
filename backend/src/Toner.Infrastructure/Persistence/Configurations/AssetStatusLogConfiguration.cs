using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class AssetStatusLogConfiguration : IEntityTypeConfiguration<AssetStatusLog>
{
    public void Configure(EntityTypeBuilder<AssetStatusLog> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("AssetStatusLogs");

        builder.Property(l => l.PreviousStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(l => l.NewStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.Asset)
            .WithMany(a => a.StatusLogs)
            .HasForeignKey(l => l.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.ChangedByUser)
            .WithMany()
            .HasForeignKey(l => l.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
