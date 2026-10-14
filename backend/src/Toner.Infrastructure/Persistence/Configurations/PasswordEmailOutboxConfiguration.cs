using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

// Sin RLS a propósito (excepción registrada en CLAUDE.md): no tiene ClientId, solo la leen los jobs con
// contexto de staff, y cada fila vive lo que tarda el despachador en encolarla (segundos).
public class PasswordEmailOutboxConfiguration : IEntityTypeConfiguration<PasswordEmailOutbox>
{
    public void Configure(EntityTypeBuilder<PasswordEmailOutbox> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("PasswordEmailOutbox");

        builder.Property(o => o.Cedula).IsRequired().HasMaxLength(20);
        builder.Property(o => o.EncryptedPassword).IsRequired();

        // El despachador lee las pendientes de la más antigua a la más nueva.
        builder.HasIndex(o => o.CreatedAt);
    }
}
