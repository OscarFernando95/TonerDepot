using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("DeviceTokens");

        builder.Property(d => d.Token).IsRequired().HasMaxLength(512);
        builder.Property(d => d.Platform).HasConversion<string>().HasMaxLength(10);

        // Un dispositivo (token) pertenece a un solo usuario, y cada usuario tiene a lo sumo un token por plataforma.
        builder.HasIndex(d => d.Token).IsUnique();
        builder.HasIndex(d => new { d.UserId, d.Platform }).IsUnique();

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
