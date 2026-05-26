using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class IntakeRecordConfiguration : IEntityTypeConfiguration<IntakeRecord>
{
    public void Configure(EntityTypeBuilder<IntakeRecord> builder)
    {
        builder.HasKey(ir => ir.Id);

        builder.Property(ir => ir.Mode)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ir => ir.DataJson)
            .HasColumnType("jsonb");

        builder.HasOne(ir => ir.Appointment)
            .WithOne(a => a.IntakeRecord)
            .HasForeignKey<IntakeRecord>(ir => ir.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
