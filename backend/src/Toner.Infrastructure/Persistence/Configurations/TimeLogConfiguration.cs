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

        // Denormalizado para RLS (fase 3b). Sin este índice la política no resuelve por
        // Index Scan y la denormalización no sirve de nada.
        builder.HasIndex(t => t.ClientId);

        builder.Property(l => l.Notes).HasMaxLength(1000);
        builder.Property(l => l.CheckInLocationStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.CheckOutLocationStatus).HasConversion<string>().HasMaxLength(20);

        // CODE_QUALITY_AUDIT.md hallazgo #11: el dashboard filtra por rango de StartTime; "TimeLog
        // abierto" (TechnicianCheckInService, AssetService.ListPendingInstallationsAsync) busca por
        // EndTime IS NULL, que a lo sumo tiene un puñado de filas a la vez — índices parciales
        // diminutos en vez de escanear toda la tabla.
        //
        // TechnicianId se deja EXPLÍCITO (además del parcial de abajo) porque, a diferencia de
        // AssetId, también se consulta SIN el filtro EndTime IS NULL
        // (TechnicianService.ListTimeLogsAsync trae el historial completo del técnico): al agregar
        // un índice explícito sobre esta columna, la convención de EF que generaba automáticamente
        // el índice pleno de la FK deja de aplicarse, así que hay que pedirlo a mano o se perdería.
        // El nombre va en el propio HasIndex(...) (no encadenado después con HasDatabaseName) porque
        // EF resuelve HasIndex(mismaExpresión) contra el MISMO IndexBuilder si no hay nombre en la
        // llamada — sin el nombre acá, la segunda llamada terminaría reconfigurando la primera en vez
        // de crear un índice aparte.
        builder.HasIndex(l => l.StartTime);
        builder.HasIndex(l => l.TechnicianId);
        builder.HasIndex(l => l.TechnicianId, "IX_TimeLogs_TechnicianId_Open").HasFilter("\"EndTime\" IS NULL");
        builder.HasIndex(l => l.AssetId).HasFilter("\"EndTime\" IS NULL");

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
