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

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);

        builder.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Client)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Technician)
            .WithOne(t => t.User)
            .HasForeignKey<Technician>(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
