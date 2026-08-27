using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class AssignmentHistoryConfiguration : IEntityTypeConfiguration<AssignmentHistory>
{
    public void Configure(EntityTypeBuilder<AssignmentHistory> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("AssignmentHistories", t => t.HasCheckConstraint(
            "CK_AssignmentHistories_ExactlyOneTarget",
            "(\"ServiceTicketId\" IS NOT NULL) <> (\"MaintenanceOrderId\" IS NOT NULL)"));

        builder.Property(a => a.AssignmentType).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Reason).HasMaxLength(500);

        // Denormalizado para RLS (fase 3b). Sin este índice la política no resuelve por
        // Index Scan y la denormalización no sirve de nada.
        builder.Property(a => a.ClientId).IsRequired();
        builder.HasIndex(a => a.ClientId);

        builder.HasOne(a => a.ServiceTicket)
            .WithMany(t => t.AssignmentHistories)
            .HasForeignKey(a => a.ServiceTicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.MaintenanceOrder)
            .WithMany(o => o.AssignmentHistories)
            .HasForeignKey(a => a.MaintenanceOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Technician)
            .WithMany()
            .HasForeignKey(a => a.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AssignedByUser)
            .WithMany()
            .HasForeignKey(a => a.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
