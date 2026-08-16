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
