using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ExtractedDataConfiguration : IEntityTypeConfiguration<ExtractedData>
{
    public void Configure(EntityTypeBuilder<ExtractedData> builder)
    {
        builder.HasKey(ed => ed.Id);

        builder.Property(ed => ed.DataCategory)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ed => ed.DataJson)
            .HasColumnType("jsonb");

        builder.HasIndex(ed => ed.DocumentId);
        builder.HasIndex(ed => ed.PatientId);

        builder.HasOne(ed => ed.Document)
            .WithMany(cd => cd.ExtractedData)
            .HasForeignKey(ed => ed.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
