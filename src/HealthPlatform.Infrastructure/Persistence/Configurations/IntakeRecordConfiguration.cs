using System.Text.Json;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class IntakeRecordConfiguration : IEntityTypeConfiguration<IntakeRecord>
{
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<IntakeRecord> builder)
    {
        builder.HasKey(ir => ir.Id);

        builder.Property(ir => ir.Mode)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ir => ir.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ir => ir.Data)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOpts),
                v => v == null ? null : JsonSerializer.Deserialize<IntakeData>(v, JsonOpts))
            .HasColumnName("data_json");

        builder.HasOne(ir => ir.Appointment)
            .WithOne(a => a.IntakeRecord)
            .HasForeignKey<IntakeRecord>(ir => ir.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
