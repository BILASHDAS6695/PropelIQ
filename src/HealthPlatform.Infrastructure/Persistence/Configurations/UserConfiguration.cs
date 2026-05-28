using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Lockout columns
        builder.Property(u => u.FailedLoginAttempts)
            .HasDefaultValue(0);

        builder.Property(u => u.LockoutEnd);
        builder.Property(u => u.CredentialExpiresAt);

        // Password history stored as a JSON array in a single jsonb column.
        builder.Property(u => u.PasswordHistory)
            .HasColumnName("password_history")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>());

        builder.HasMany(u => u.AuditLogs)
            .WithOne(al => al.User)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notification preferences stored as a JSONB blob.
        // Deserialisation defaults to all-enabled when the column is '{}' or null.
        builder.Property(u => u.NotificationPreferences)
            .HasColumnName("notification_preferences")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v,
                    (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<
                    HealthPlatform.Domain.ValueObjects.NotificationPreferences>(
                    v,
                    (System.Text.Json.JsonSerializerOptions?)null)
                    ?? new HealthPlatform.Domain.ValueObjects.NotificationPreferences());
    }
}
