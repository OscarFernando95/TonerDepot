using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class ServiceTicketConfiguration : IEntityTypeConfiguration<ServiceTicket>
{
    public void Configure(EntityTypeBuilder<ServiceTicket> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("ServiceTickets");

        builder.Property(t => t.Description).IsRequired().HasMaxLength(2000);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.ExternalAssetBrand).HasMaxLength(100);
        builder.Property(t => t.ExternalAssetModel).HasMaxLength(150);

        // CODE_QUALITY_AUDIT.md hallazgo #11: los listados ordenan por CreatedAt DESC sin índice de
        // soporte; el dashboard filtra por Status y por ResolvedAt (este último acotado a las filas
        // resueltas — la mayoría de las filas de un ticket viejo nunca tiene ResolvedAt).
        builder.HasIndex(t => t.CreatedAt);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.ResolvedAt).HasFilter("\"ResolvedAt\" IS NOT NULL");

        builder.HasOne(t => t.ClientLocation)
            .WithMany(l => l.ServiceTickets)
            .HasForeignKey(t => t.ClientLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Asset)
            .WithMany(a => a.ServiceTickets)
            .HasForeignKey(t => t.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ReportedByUser)
            .WithMany()
            .HasForeignKey(t => t.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Technician)
            .WithMany(tech => tech.AssignedTickets)
            .HasForeignKey(t => t.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
