using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
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

        builder.HasData(GenerateSeedRecords());
    }

    private static InsuranceRecord[] GenerateSeedRecords()
    {
        var carriers = new[]
        {
            "BlueCross BlueShield", "Aetna Health", "United Healthcare",
            "Cigna Medical", "Humana Insurance", "Anthem BCBS",
            "Molina Healthcare", "Centene Corporation", "WellCare Health",
            "Kaiser Permanente"
        };

        var records = new List<InsuranceRecord>();
        var baseIndex = 1;

        foreach (var carrier in carriers)
        {
            for (var i = 1; i <= 5; i++)
            {
                records.Add(new InsuranceRecord
                {
                    Id           = Guid.Parse($"22222222-0000-0000-0000-{baseIndex:D12}"),
                    ProviderName = carrier,
                    MemberId     = $"MBR-{baseIndex:D6}",
                    Status       = baseIndex % 7 == 0 ? InsuranceStatus.Inactive
                                                      : InsuranceStatus.Active
                });
                baseIndex++;
            }
        }

        return [.. records];
    }
}
