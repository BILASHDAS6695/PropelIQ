using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PatientView360Configuration : IEntityTypeConfiguration<PatientView360>
{
    public void Configure(EntityTypeBuilder<PatientView360> builder)
    {
        builder.HasKey(pv => pv.Id);

        builder.HasIndex(pv => pv.PatientId).IsUnique();

        builder.Property(pv => pv.ConsolidatedDataJson)
            .HasColumnType("jsonb");

        builder.HasOne(pv => pv.Patient)
            .WithOne(p => p.PatientView360)
            .HasForeignKey<PatientView360>(pv => pv.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
