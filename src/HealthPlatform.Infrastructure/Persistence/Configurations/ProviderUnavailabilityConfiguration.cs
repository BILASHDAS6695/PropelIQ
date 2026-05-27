using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ProviderUnavailabilityConfiguration
    : IEntityTypeConfiguration<ProviderUnavailability>
{
    public void Configure(EntityTypeBuilder<ProviderUnavailability> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Reason).HasMaxLength(500);

        // Unique: one unavailability record per (provider, date).
        builder.HasIndex(u => new { u.ProviderId, u.UnavailableDate })
            .IsUnique();

        builder.HasOne(u => u.Provider)
            .WithMany(p => p.Unavailabilities)
            .HasForeignKey(u => u.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
