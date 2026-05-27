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

        builder.Property(a => a.VisitReason).HasMaxLength(500);

        // Store enum as string so the DB column is human-readable.
        builder.Property(a => a.CancellationReason)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(a => a.CancellationNote).HasMaxLength(500);

        builder.Property(a => a.ConflictOverrideReason).HasMaxLength(500);
        // IsConflictOverride is a non-nullable bool; EF maps to boolean column (default false).

        builder.HasIndex(a => a.PatientId);
        builder.HasIndex(a => a.ProviderId);

        // Filtered unique index: only non-null SlotId values must be unique.
        // NULL values (walk-ins) are excluded from the constraint.
        builder.HasIndex(a => a.SlotId)
            .IsUnique()
            .HasFilter("slot_id IS NOT NULL");

        builder.HasIndex(a => new { a.PatientId, a.ProviderId, a.SlotTime });

        // Index to support fast provider daily-queue queries.
        builder.HasIndex(a => new { a.ProviderId, a.ArrivalTime });

        builder.HasOne(a => a.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Provider)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional relationship: walk-in appointments have no slot.
        builder.HasOne(a => a.Slot)
            .WithOne(s => s.Appointment)
            .HasForeignKey<Appointment>(a => a.SlotId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // PostgreSQL xmin system column as optimistic-concurrency token (Npgsql 8.x)
        builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
