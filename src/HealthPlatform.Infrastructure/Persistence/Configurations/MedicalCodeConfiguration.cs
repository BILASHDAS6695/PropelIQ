using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class MedicalCodeConfiguration : IEntityTypeConfiguration<MedicalCode>
{
    public void Configure(EntityTypeBuilder<MedicalCode> builder)
    {
        builder.HasKey(mc => mc.Id);

        builder.Property(mc => mc.CodeType)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(mc => mc.Code).IsRequired().HasMaxLength(20);
        builder.Property(mc => mc.Description).IsRequired().HasMaxLength(500);

        builder.HasIndex(mc => mc.PatientViewId);

        builder.HasOne(mc => mc.PatientView)
            .WithMany(pv => pv.MedicalCodes)
            .HasForeignKey(mc => mc.PatientViewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
