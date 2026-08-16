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
            "(CASE WHEN \"ServiceTicketId\" IS NOT NULL THEN 1 ELSE 0 END + " +
            "CASE WHEN \"MaintenanceOrderId\" IS NOT NULL THEN 1 ELSE 0 END + " +
            "CASE WHEN \"AssetId\" IS NOT NULL THEN 1 ELSE 0 END) = 1"));

        builder.Property(l => l.Notes).HasMaxLength(1000);

        builder.HasOne(l => l.ServiceTicket)
            .WithMany(t => t.TimeLogs)
            .HasForeignKey(l => l.ServiceTicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.MaintenanceOrder)
            .WithMany(o => o.TimeLogs)
            .HasForeignKey(l => l.MaintenanceOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Asset)
            .WithMany(a => a.TimeLogs)
            .HasForeignKey(l => l.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Technician)
            .WithMany(t => t.TimeLogs)
            .HasForeignKey(l => l.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
