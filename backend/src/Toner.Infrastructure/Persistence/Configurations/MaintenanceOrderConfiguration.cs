using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class MaintenanceOrderConfiguration : IEntityTypeConfiguration<MaintenanceOrder>
{
    // Expuesto para que TonerDbContext traduzca su violación (23505) a un mensaje legible.
    public const string OpenByScheduleIndexName = "IX_MaintenanceOrders_MaintenanceScheduleId_Open";
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
        builder.Property(o => o.Description).HasMaxLength(1000);

        // CODE_QUALITY_AUDIT.md hallazgo #11: los listados ordenan por CreatedAt DESC sin índice de
        // soporte; el dashboard filtra por CompletedAt (acotado a las órdenes ya completadas).
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.CompletedAt).HasFilter("\"CompletedAt\" IS NOT NULL");

        // MANT-1: una sola orden abierta por cronograma, garantizada en la base. Las comprobaciones "¿ya hay una
        // abierta?" de MaintenanceScheduleEngine y MaintenanceOrderService.PrepareManualAsync son check-then-act: el
        // job diario y un técnico que inicia su mantenimiento pueden pasarlas a la vez sobre el mismo equipo. Con
        // este índice el segundo en guardar recibe un 409 en vez de dejar dos órdenes abiertas.
        // El índice parcial solo cubre las abiertas; el historial completo de un cronograma (ListByScheduleAsync) sigue
        // necesitando el índice de la FK, que EF descartaría por "cubierto" si no se declara aquí.
        builder.HasIndex(o => o.MaintenanceScheduleId);
        builder.HasIndex(o => o.MaintenanceScheduleId, OpenByScheduleIndexName)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Pendiente', 'Asignada', 'EnProceso')");

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
