using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class ExceptionLogConfiguration : IEntityTypeConfiguration<ExceptionLog>
{
    public void Configure(EntityTypeBuilder<ExceptionLog> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("ExceptionLogs");

        builder.Property(e => e.Source).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ExceptionType).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.RequestMethod).HasMaxLength(10);
        builder.Property(e => e.RequestPath).HasMaxLength(500);
        builder.Property(e => e.UserEmail).HasMaxLength(320);

        // Para poder listar/filtrar por fecha sin tener que escanear toda la tabla a medida que crece.
        builder.HasIndex(e => e.CreatedAt);
    }
}
