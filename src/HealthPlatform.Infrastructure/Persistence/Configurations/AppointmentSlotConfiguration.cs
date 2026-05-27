using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentSlotConfiguration : IEntityTypeConfiguration<AppointmentSlot>
{
    public void Configure(EntityTypeBuilder<AppointmentSlot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(SlotStatus.Available);

        builder.HasIndex(s => new { s.ProviderId, s.StartTime });
        builder.HasIndex(s => s.Status);

        builder.HasOne(s => s.Provider)
            .WithMany(p => p.AppointmentSlots)
            .HasForeignKey(s => s.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        // PostgreSQL xmin system column as optimistic-concurrency token.
        // Allows EF to detect concurrent slot-status changes and throw
        // DbUpdateConcurrencyException ("first wins" booking race).
        builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
