using Toner.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ToTable("Clients");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.TaxId).HasMaxLength(50);
        builder.Property(c => c.ContactName).HasMaxLength(200);
        builder.Property(c => c.ContactEmail).HasMaxLength(256);
        builder.Property(c => c.ContactPhone).HasMaxLength(50);
        builder.Property(c => c.IsContractClient).HasDefaultValue(true);
        builder.Property(c => c.SupportCoverage).HasConversion<string>().HasMaxLength(20).HasDefaultValue(SupportCoverage.HorarioOficina);
    }
}
