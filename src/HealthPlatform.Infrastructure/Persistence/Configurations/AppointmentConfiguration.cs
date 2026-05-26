using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(a => a.PatientId);
        builder.HasIndex(a => a.ProviderId);
        builder.HasIndex(a => a.SlotId).IsUnique();

        builder.HasOne(a => a.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Provider)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Slot)
            .WithOne(s => s.Appointment)
            .HasForeignKey<Appointment>(a => a.SlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
