using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("Users");

        // Ya no es la credencial de login (eso es Cedula) — queda como dato de contacto opcional.
        builder.Property(u => u.Email).HasMaxLength(256);

        builder.Property(u => u.Cedula).IsRequired().HasMaxLength(20);
        builder.HasIndex(u => u.Cedula).IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Phone).HasMaxLength(30);
        builder.Property(u => u.Address).HasMaxLength(300);
        builder.Property(u => u.MustChangePassword).IsRequired().HasDefaultValue(false);
        builder.Property(u => u.FailedLoginAttempts).IsRequired().HasDefaultValue(0);

        builder.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Client)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.City)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Technician)
            .WithOne(t => t.User)
            .HasForeignKey<Technician>(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
