using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class DataConflictConfiguration : IEntityTypeConfiguration<DataConflict>
{
    public void Configure(EntityTypeBuilder<DataConflict> builder)
    {
        builder.HasKey(dc => dc.Id);

        builder.Property(dc => dc.Field).IsRequired().HasMaxLength(200);
        builder.Property(dc => dc.ValueA).IsRequired().HasMaxLength(1000);
        builder.Property(dc => dc.ValueB).IsRequired().HasMaxLength(1000);

        builder.Property(dc => dc.Severity)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(dc => dc.ResolutionStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(dc => dc.PatientViewId);

        builder.HasOne(dc => dc.PatientView)
            .WithMany(pv => pv.DataConflicts)
            .HasForeignKey(dc => dc.PatientViewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
