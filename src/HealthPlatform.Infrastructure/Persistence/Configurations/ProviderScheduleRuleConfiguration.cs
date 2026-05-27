using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ProviderScheduleRuleConfiguration
    : IEntityTypeConfiguration<ProviderScheduleRule>
{
    public void Configure(EntityTypeBuilder<ProviderScheduleRule> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.DayOfWeek)
            .HasConversion<int>();

        builder.Property(r => r.SlotDurationMinutes)
            .HasDefaultValue(30);

        // Unique: one rule per (provider, day-of-week).
        // Overlapping day-of-week rules for the same provider are rejected at creation time.
        builder.HasIndex(r => new { r.ProviderId, r.DayOfWeek })
            .IsUnique();

        builder.HasOne(r => r.Provider)
            .WithMany(p => p.ScheduleRules)
            .HasForeignKey(r => r.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
