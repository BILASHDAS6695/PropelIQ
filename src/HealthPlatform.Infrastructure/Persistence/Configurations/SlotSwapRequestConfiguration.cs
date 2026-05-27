using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class SlotSwapRequestConfiguration
    : IEntityTypeConfiguration<SlotSwapRequest>
{
    public void Configure(EntityTypeBuilder<SlotSwapRequest> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.CancellationReason)
            .HasMaxLength(500);

        // ── Staff mediation properties (US-030) ───────────────────────────
        builder.Property(r => r.OverrideReason)
            .HasMaxLength(500);

        builder.Property(r => r.OverriddenAt)
            .IsRequired(false);

        builder.Property(r => r.MediatedByUserId)
            .IsRequired(false);

        builder.Property(r => r.ThreeWayNewTargetSlotId)
            .IsRequired(false);

        // Optimistic concurrency token — maps to PostgreSQL's xmin system column.
        // EF Core includes xmin in UPDATE WHERE clauses; a mismatch raises
        // DbUpdateConcurrencyException when two staff members mediate simultaneously.
        builder.Property(r => r.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // ── Filtered unique index: only one active (Pending) swap request ──
        // per requester appointment. Completed/cancelled/expired requests are
        // excluded so historical records can accumulate.
        builder.HasIndex(r => r.RequesterAppointmentId)
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_slot_swap_requests_active_per_appointment");

        // ── Support fast expiry-sweep queries ─────────────────────────────
        builder.HasIndex(r => new { r.Status, r.ExpiresAt })
            .HasDatabaseName("ix_slot_swap_requests_status_expires");

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasOne(r => r.RequesterPatient)
            .WithMany()
            .HasForeignKey(r => r.RequesterPatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.RequesterAppointment)
            .WithMany(a => a.InitiatedSwapRequests)
            .HasForeignKey(r => r.RequesterAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TargetAppointment)
            .WithMany(a => a.ReceivedSwapRequests)
            .HasForeignKey(r => r.TargetAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
