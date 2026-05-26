using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ClinicalDocumentConfiguration : IEntityTypeConfiguration<ClinicalDocument>
{
    public void Configure(EntityTypeBuilder<ClinicalDocument> builder)
    {
        builder.HasKey(cd => cd.Id);

        builder.Property(cd => cd.FileName).IsRequired().HasMaxLength(500);
        builder.Property(cd => cd.StoragePath).IsRequired().HasMaxLength(1000);

        builder.Property(cd => cd.ProcessingStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(cd => cd.PatientId);

        builder.HasOne(cd => cd.Patient)
            .WithMany(p => p.ClinicalDocuments)
            .HasForeignKey(cd => cd.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
