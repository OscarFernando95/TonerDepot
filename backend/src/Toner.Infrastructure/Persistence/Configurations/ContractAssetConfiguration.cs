using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class ContractAssetConfiguration : IEntityTypeConfiguration<ContractAsset>
{
    public void Configure(EntityTypeBuilder<ContractAsset> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("ContractAssets");

        builder.HasOne(ca => ca.Contract)
            .WithMany(c => c.ContractAssets)
            .HasForeignKey(ca => ca.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ca => ca.Asset)
            .WithMany(a => a.ContractAssets)
            .HasForeignKey(ca => ca.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
