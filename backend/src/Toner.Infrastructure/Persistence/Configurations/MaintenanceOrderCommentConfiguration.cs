using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class MaintenanceOrderCommentConfiguration : IEntityTypeConfiguration<MaintenanceOrderComment>
{
    public void Configure(EntityTypeBuilder<MaintenanceOrderComment> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("MaintenanceOrderComments");

        builder.Property(c => c.AuthorRole).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Body).IsRequired().HasMaxLength(1000);

        builder.HasIndex(c => new { c.MaintenanceOrderId, c.CreatedAt });
        // Índice del predicado de la política RLS (sargable).
        builder.HasIndex(c => c.ClientId);

        builder.HasOne(c => c.MaintenanceOrder)
            .WithMany(o => o.Comments)
            .HasForeignKey(c => c.MaintenanceOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.AuthorUser)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
