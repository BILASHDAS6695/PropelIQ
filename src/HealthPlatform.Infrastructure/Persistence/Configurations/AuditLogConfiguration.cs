using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(al => al.Id);

        builder.Property(al => al.Action).IsRequired().HasMaxLength(200);
        builder.Property(al => al.EntityType).IsRequired().HasMaxLength(200);
        builder.Property(al => al.CurrentHash).IsRequired().HasMaxLength(64);
        builder.Property(al => al.PreviousHash).HasMaxLength(64);

        builder.Property(al => al.Details)
            .HasColumnType("jsonb");

        builder.HasIndex(al => al.UserId);
        builder.HasIndex(al => new { al.EntityType, al.EntityId });

        builder.HasOne(al => al.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
