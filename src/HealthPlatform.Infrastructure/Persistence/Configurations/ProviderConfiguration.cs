using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    private static readonly DateTimeOffset SeedDate =
        new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Specialty).HasMaxLength(100);

        builder.HasData(
            new Provider
            {
                Id        = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                Name      = "Dr. Sarah Mitchell",
                Specialty = "Cardiology",
                CreatedAt = SeedDate,
                UpdatedAt = SeedDate,
                IsDeleted = false
            },
            new Provider
            {
                Id        = Guid.Parse("11111111-0000-0000-0000-000000000002"),
                Name      = "Dr. James Okafor",
                Specialty = "General Practice",
                CreatedAt = SeedDate,
                UpdatedAt = SeedDate,
                IsDeleted = false
            },
            new Provider
            {
                Id        = Guid.Parse("11111111-0000-0000-0000-000000000003"),
                Name      = "Dr. Priya Sharma",
                Specialty = "Neurology",
                CreatedAt = SeedDate,
                UpdatedAt = SeedDate,
                IsDeleted = false
            },
            new Provider
            {
                Id        = Guid.Parse("11111111-0000-0000-0000-000000000004"),
                Name      = "Dr. Marcus Chen",
                Specialty = "Orthopedics",
                CreatedAt = SeedDate,
                UpdatedAt = SeedDate,
                IsDeleted = false
            },
            new Provider
            {
                Id        = Guid.Parse("11111111-0000-0000-0000-000000000005"),
                Name      = "Dr. Fatima Al-Rashid",
                Specialty = "Pediatrics",
                CreatedAt = SeedDate,
                UpdatedAt = SeedDate,
                IsDeleted = false
            }
        );
    }
}
