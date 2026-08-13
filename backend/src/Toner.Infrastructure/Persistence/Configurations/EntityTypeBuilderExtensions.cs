using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Common;

namespace Toner.Infrastructure.Persistence.Configurations;

internal static class EntityTypeBuilderExtensions
{
    // BaseEntity.Id se genera en el cliente (Guid.NewGuid()), no en la base de datos:
    // necesario para que la app Flutter cree registros offline y los sincronice sin colisión de IDs.
    public static void ConfigureBaseEntity<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : BaseEntity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
    }
}
