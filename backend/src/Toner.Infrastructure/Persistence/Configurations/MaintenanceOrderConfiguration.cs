using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class MaintenanceOrderConfiguration : IEntityTypeConfiguration<MaintenanceOrder>
{
    public void Configure(EntityTypeBuilder<MaintenanceOrder> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("MaintenanceOrders");

        // Denormalizado para RLS (fase 3b). Sin este índice la política no resuelve por
        // Index Scan y la denormalización no sirve de nada.
        builder.Property(o => o.ClientId).IsRequired();
        builder.HasIndex(o => o.ClientId);

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Reason).HasMaxLength(500);

        // CODE_QUALITY_AUDIT.md hallazgo #11: los listados ordenan por CreatedAt DESC sin índice de
        // soporte; el dashboard filtra por CompletedAt (acotado a las órdenes ya completadas).
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.CompletedAt).HasFilter("\"CompletedAt\" IS NOT NULL");

        builder.HasOne(o => o.MaintenanceSchedule)
            .WithMany(s => s.MaintenanceOrders)
            .HasForeignKey(o => o.MaintenanceScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Asset)
            .WithMany()
            .HasForeignKey(o => o.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Technician)
            .WithMany(t => t.AssignedMaintenanceOrders)
            .HasForeignKey(o => o.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
