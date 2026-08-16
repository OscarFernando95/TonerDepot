using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("Assets");

        builder.Property(a => a.SerialNumber).IsRequired().HasMaxLength(100);
        builder.HasIndex(a => a.SerialNumber).IsUnique();

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.LifecycleStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Area).HasMaxLength(150);

        builder.HasOne(a => a.AssetModel)
            .WithMany(m => m.Assets)
            .HasForeignKey(a => a.AssetModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CurrentClientLocation)
            .WithMany(l => l.Assets)
            .HasForeignKey(a => a.CurrentClientLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
