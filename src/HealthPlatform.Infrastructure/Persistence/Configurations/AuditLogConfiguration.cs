using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(al => al.Id);

        builder.Property(al => al.Id)
            .ValueGeneratedNever(); // Id set by AuditInterceptor before insert

        builder.Property(al => al.UserId).IsRequired(false);

        builder.Property(al => al.Action)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(al => al.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(al => al.CurrentHash).IsRequired().HasMaxLength(64);
        builder.Property(al => al.PreviousHash).HasMaxLength(64);

        builder.Property(al => al.Details)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.HasIndex(al => al.UserId).HasDatabaseName("ix_audit_logs_user_id");
        builder.HasIndex(al => al.EntityId).HasDatabaseName("ix_audit_logs_entity_id");
        builder.HasIndex(al => al.Timestamp).HasDatabaseName("ix_audit_logs_timestamp");

        // Optional FK to User — nullable because system operations have no authenticated user
        builder.HasOne<User>()
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(al => al.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
