using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("Evidences", t => t.HasCheckConstraint(
            "CK_Evidences_ExactlyOneTarget",
            "(\"ServiceTicketId\" IS NOT NULL) <> (\"MaintenanceOrderId\" IS NOT NULL)"));

        builder.Property(e => e.FileUrl).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(300);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);

        builder.HasOne(e => e.ServiceTicket)
            .WithMany(t => t.Evidences)
            .HasForeignKey(e => e.ServiceTicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.MaintenanceOrder)
            .WithMany(o => o.Evidences)
            .HasForeignKey(e => e.MaintenanceOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UploadedByUser)
            .WithMany()
            .HasForeignKey(e => e.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
