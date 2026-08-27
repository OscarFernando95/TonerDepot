using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("MaintenanceSchedules");

        // Denormalizado para RLS (fase 3b). Sin este índice la política no resuelve por
        // Index Scan y la denormalización no sirve de nada.
        builder.Property(s => s.ClientId).IsRequired();
        builder.HasIndex(s => s.ClientId);

        // Un cronograma por activo — reinstalar (bodega -> nuevo contrato) reinicia el mismo registro,
        // ver MaintenanceScheduleEngine.UpsertForInstallationAsync.
        builder.HasIndex(s => s.AssetId).IsUnique();

        builder.HasOne(s => s.Asset)
            .WithMany(a => a.MaintenanceSchedules)
            .HasForeignKey(s => s.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Contract)
            .WithMany(c => c.MaintenanceSchedules)
            .HasForeignKey(s => s.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
