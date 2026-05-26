using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PreferredSlotPreferenceConfiguration
    : IEntityTypeConfiguration<PreferredSlotPreference>
{
    public void Configure(EntityTypeBuilder<PreferredSlotPreference> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(p => p.Appointment)
            .WithOne(a => a.PreferredSlotPreference)
            .HasForeignKey<PreferredSlotPreference>(p => p.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
