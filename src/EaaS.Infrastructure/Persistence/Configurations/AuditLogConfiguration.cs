using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.AdminUserId)
            .HasColumnName("admin_user_id")
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("action");

        builder.Property(e => e.TargetType)
            .HasColumnName("target_type")
            .HasMaxLength(100);

        builder.Property(e => e.TargetId)
            .HasColumnName("target_id")
            .HasMaxLength(255);

        builder.Property(e => e.Details)
            .HasColumnName("details")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(e => e.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(e => e.AdminUserId)
            .HasDatabaseName("idx_audit_logs_admin_user");

        builder.HasIndex(e => e.Action)
            .HasDatabaseName("idx_audit_logs_action");

        builder.HasIndex(e => e.CreatedAt)
            .IsDescending()
            .HasDatabaseName("idx_audit_logs_created_at");
    }
}
