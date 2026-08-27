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

        // CODE_QUALITY_AUDIT.md hallazgo #11: filtrado por LifecycleStatus en 3 listados
        // (AssetService.ListPendingInstallationsAsync, ListForMeterReadingAsync,
        // MaintenanceScheduleService.BackfillMissingAsync) sin índice de soporte.
        builder.HasIndex(a => a.LifecycleStatus);

        // Denormalizado y mantenido por el trigger assets_sync_client_id (ver la migración
        // AddPhase3aDenormalizedClientId). La aplicación NUNCA lo escribe: se mapea como generado por
        // la base para que EF no lo mande en el INSERT/UPDATE y lo lea de vuelta con RETURNING — si
        // EF lo enviara, sobreescribiría en memoria el valor que calculó el trigger.
        builder.Property(a => a.ClientId).ValueGeneratedOnAddOrUpdate();

        // Sin este índice la política RLS no resuelve por Index Scan y todo el trabajo de
        // denormalización no sirve de nada.
        builder.HasIndex(a => a.ClientId);

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
