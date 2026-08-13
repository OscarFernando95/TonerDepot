using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class AssetBrandConfiguration : IEntityTypeConfiguration<AssetBrand>
{
    public void Configure(EntityTypeBuilder<AssetBrand> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("AssetBrands");

        builder.Property(b => b.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(b => b.Name).IsUnique();
    }
}
