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

        // Denormalizado para RLS (fase 3b). Sin este índice la política no resuelve por
        // Index Scan y la denormalización no sirve de nada.
        builder.Property(ca => ca.ClientId).IsRequired();
        builder.HasIndex(ca => ca.ClientId);

        builder.HasOne(ca => ca.Contract)
            .WithMany(c => c.ContractAssets)
            .HasForeignKey(ca => ca.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ca => ca.Asset)
            .WithMany(a => a.ContractAssets)
            .HasForeignKey(ca => ca.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Backstop a nivel de base de datos para "un activo no puede tener dos vínculos de contrato
        // activos al mismo tiempo": el chequeo equivalente en ContractAssetService.AddAsync es
        // verificar-y-luego-insertar y por sí solo no evita la condición de carrera entre dos
        // requests concurrentes. El índice único parcial (solo sobre filas con EndDate NULL) hace que
        // la segunda inserción falle con una violación de único real, que TonerDbContext traduce a
        // ConflictException en vez de dejar corromper el dato en silencio.
        builder.HasIndex(ca => ca.AssetId)
            .IsUnique()
            .HasFilter("\"EndDate\" IS NULL");
    }
}
