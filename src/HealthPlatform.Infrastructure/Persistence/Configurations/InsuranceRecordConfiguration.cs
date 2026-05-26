using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class InsuranceRecordConfiguration : IEntityTypeConfiguration<InsuranceRecord>
{
    public void Configure(EntityTypeBuilder<InsuranceRecord> builder)
    {
        builder.HasKey(ir => ir.Id);

        builder.Property(ir => ir.ProviderName).IsRequired().HasMaxLength(200);
        builder.Property(ir => ir.MemberId).IsRequired().HasMaxLength(100);

        builder.Property(ir => ir.Status)
            .HasConversion<string>()
            .HasMaxLength(10);
    }
}
