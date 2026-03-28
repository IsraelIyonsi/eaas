using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.KeyHash)
            .HasColumnName("key_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.Prefix)
            .HasColumnName("prefix")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(a => a.AllowedDomains)
            .HasColumnName("allowed_domains")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'");

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasDefaultValue(ApiKeyStatus.Active);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(a => a.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(a => a.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(a => a.RotatingExpiresAt)
            .HasColumnName("rotating_expires_at");

        builder.Property(a => a.ReplacedByKeyId)
            .HasColumnName("replaced_by_key_id");

        // Unique constraint on key_hash
        builder.HasIndex(a => a.KeyHash)
            .IsUnique()
            .HasDatabaseName("uq_api_keys_key_hash");

        // Indexes
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("idx_api_keys_tenant");

        builder.HasIndex(a => new { a.TenantId, a.Status })
            .HasFilter("status = 'active'")
            .HasDatabaseName("idx_api_keys_status");

        builder.HasMany(a => a.Emails)
            .WithOne(e => e.ApiKey)
            .HasForeignKey(e => e.ApiKeyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
