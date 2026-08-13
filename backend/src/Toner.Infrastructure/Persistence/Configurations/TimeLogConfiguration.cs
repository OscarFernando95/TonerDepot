using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class TimeLogConfiguration : IEntityTypeConfiguration<TimeLog>
{
    public void Configure(EntityTypeBuilder<TimeLog> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("TimeLogs", t => t.HasCheckConstraint(
            "CK_TimeLogs_ExactlyOneTarget",
            "(\"ServiceTicketId\" IS NOT NULL) <> (\"MaintenanceOrderId\" IS NOT NULL)"));

        builder.Property(l => l.Notes).HasMaxLength(1000);

        builder.HasOne(l => l.ServiceTicket)
            .WithMany(t => t.TimeLogs)
            .HasForeignKey(l => l.ServiceTicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.MaintenanceOrder)
            .WithMany(o => o.TimeLogs)
            .HasForeignKey(l => l.MaintenanceOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Technician)
            .WithMany(t => t.TimeLogs)
            .HasForeignKey(l => l.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
