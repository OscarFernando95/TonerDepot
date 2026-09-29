using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class TechnicianAssetConfiguration : IEntityTypeConfiguration<TechnicianAsset>
{
    public void Configure(EntityTypeBuilder<TechnicianAsset> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("TechnicianAssets");

        builder.HasIndex(ta => new { ta.TechnicianId, ta.AssetId }).IsUnique();

        builder.HasOne(ta => ta.Technician)
            .WithMany(t => t.LinkedAssets)
            .HasForeignKey(ta => ta.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ta => ta.Asset)
            .WithMany(a => a.TechnicianAssets)
            .HasForeignKey(ta => ta.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
