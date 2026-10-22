using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class AssetModelConfiguration : IEntityTypeConfiguration<AssetModel>
{
    public void Configure(EntityTypeBuilder<AssetModel> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("AssetModels");

        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(m => new { m.AssetBrandId, m.Name }).IsUnique();

        // Default en la base: los inserts SQL directos (seeds, pruebas) que no conocen la columna siguen siendo válidos.
        builder.Property(m => m.RentalRetirementWarningPrints).HasDefaultValue(50000);

        builder.HasOne(m => m.AssetBrand)
            .WithMany(b => b.Models)
            .HasForeignKey(m => m.AssetBrandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
