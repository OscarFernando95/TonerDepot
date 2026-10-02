using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Infrastructure.Persistence.Configurations;

// Inventario: datos de la empresa (no de un cliente), así que como Cities/Zones no llevan RLS. El consumo ligado a
// un activo/cliente (fase 3) sí tendrá ClientId y política.
public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("InventoryItems");

        builder.Property(i => i.Name).IsRequired().HasMaxLength(150);
        builder.HasIndex(i => i.Name).IsUnique();
        builder.Property(i => i.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Unit).HasMaxLength(30);
        builder.Property(i => i.UnitCost).HasPrecision(14, 2);
    }
}

public class InventoryLocationConfiguration : IEntityTypeConfiguration<InventoryLocation>
{
    public void Configure(EntityTypeBuilder<InventoryLocation> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("InventoryLocations");

        builder.Property(l => l.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.Name).HasMaxLength(150);
        builder.Property(l => l.Address).HasMaxLength(300);

        // Una ubicación por zona, y una sola principal.
        builder.HasIndex(l => l.ZoneId).IsUnique().HasFilter("\"ZoneId\" IS NOT NULL");
        builder.HasIndex(l => l.Kind).IsUnique().HasFilter("\"Kind\" = 'Principal'");

        builder.HasOne(l => l.Zone).WithMany().HasForeignKey(l => l.ZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.City).WithMany().HasForeignKey(l => l.CityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("InventoryMovements");

        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Notes).HasMaxLength(500);

        // Saldo por (ubicación, ítem) y listado cronológico.
        builder.HasIndex(m => new { m.InventoryLocationId, m.InventoryItemId });
        builder.HasIndex(m => new { m.OccurredAt, m.Id });
        builder.HasIndex(m => m.ClientId);
        builder.HasIndex(m => new { m.AssetId, m.OccurredAt }).HasFilter("\"AssetId\" IS NOT NULL");

        builder.HasOne<Asset>().WithMany().HasForeignKey(m => m.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MaintenanceOrder>().WithMany().HasForeignKey(m => m.MaintenanceOrderId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ServiceTicket>().WithMany().HasForeignKey(m => m.ServiceTicketId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<TimeLog>().WithMany().HasForeignKey(m => m.TimeLogId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.InventoryItem).WithMany().HasForeignKey(m => m.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.InventoryLocation).WithMany().HasForeignKey(m => m.InventoryLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BrandBaseItemConfiguration : IEntityTypeConfiguration<BrandBaseItem>
{
    public void Configure(EntityTypeBuilder<BrandBaseItem> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("BrandBaseItems");

        builder.Property(b => b.GroupName).IsRequired().HasMaxLength(100);
        builder.HasIndex(b => new { b.AssetBrandId, b.InventoryItemId }).IsUnique();

        builder.HasOne(b => b.AssetBrand).WithMany().HasForeignKey(b => b.AssetBrandId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(b => b.InventoryItem).WithMany().HasForeignKey(b => b.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ModelBaseItemConfiguration : IEntityTypeConfiguration<ModelBaseItem>
{
    public void Configure(EntityTypeBuilder<ModelBaseItem> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("ModelBaseItems");

        builder.Property(m => m.GroupName).HasMaxLength(100);
        builder.HasIndex(m => new { m.AssetModelId, m.InventoryItemId }).IsUnique();

        builder.HasOne(m => m.AssetModel).WithMany().HasForeignKey(m => m.AssetModelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.InventoryItem).WithMany().HasForeignKey(m => m.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
