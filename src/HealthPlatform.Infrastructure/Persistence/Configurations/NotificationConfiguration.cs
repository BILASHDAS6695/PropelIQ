using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Channel)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(n => n.DeliveryStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(n => n.PatientId);

        builder.HasOne(n => n.Patient)
            .WithMany(p => p.Notifications)
            .HasForeignKey(n => n.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Appointment)
            .WithMany(a => a.Notifications)
            .HasForeignKey(n => n.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
