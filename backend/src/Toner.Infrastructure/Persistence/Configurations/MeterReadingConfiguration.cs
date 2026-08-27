using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("MeterReadings");

        // Denormalizado para RLS (fase 3b). Sin este índice la política no resuelve por
        // Index Scan y la denormalización no sirve de nada.
        builder.HasIndex(m => m.ClientId);

        // "Última lectura por activo" es la consulta más repetida del sistema (CODE_QUALITY_AUDIT.md
        // hallazgo #3) — sin este compuesto, cada búsqueda hace index scan por AssetId + sort en memoria.
        builder.HasIndex(m => new { m.AssetId, m.ReadingDate });

        builder.HasOne(m => m.Asset)
            .WithMany(a => a.MeterReadings)
            .HasForeignKey(m => m.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.RegisteredByUser)
            .WithMany()
            .HasForeignKey(m => m.RegisteredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
