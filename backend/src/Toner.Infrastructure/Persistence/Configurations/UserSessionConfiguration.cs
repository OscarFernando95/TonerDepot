using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    // Nombre expuesto para que Application pueda reconocer la violación de unicidad sin conocer Npgsql.
    public const string SingleActiveSessionIndexName = "IX_UserSessions_UserId_SingleActive";

    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("UserSessions");

        builder.Property(s => s.ClientType).IsRequired().HasMaxLength(20);
        builder.Property(s => s.IpAddress).HasMaxLength(64);
        builder.Property(s => s.UserAgent).HasMaxLength(300);
        builder.Property(s => s.RevokedReason).HasMaxLength(60);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.UserId, s.IssuedAt });

        // Sesión única: como máximo UNA fila viva con EnforcesSingleSession por usuario, garantizado
        // por la base y no por un "check-then-insert" (que dos logins simultáneos se saltarían).
        builder.HasIndex(s => s.UserId)
            .IsUnique()
            .HasDatabaseName(SingleActiveSessionIndexName)
            .HasFilter("\"RevokedAt\" IS NULL AND \"EnforcesSingleSession\" = TRUE");
    }
}
