using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class SuppressionEntryConfiguration : IEntityTypeConfiguration<SuppressionEntry>
{
    public void Configure(EntityTypeBuilder<SuppressionEntry> builder)
    {
        builder.ToTable("suppression_list");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.EmailAddress)
            .HasColumnName("email_address")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(s => s.Reason)
            .HasColumnName("reason");

        builder.Property(s => s.SourceMessageId)
            .HasColumnName("source_message_id")
            .HasMaxLength(32);

        builder.Property(s => s.SuppressedAt)
            .HasColumnName("suppressed_at")
            .HasDefaultValueSql("NOW()");

        // Unique constraint on (tenant_id, email_address)
        builder.HasIndex(s => new { s.TenantId, s.EmailAddress })
            .IsUnique()
            .HasDatabaseName("uq_suppression_tenant_email");

        // Indexes
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("idx_suppression_tenant");

        builder.HasIndex(s => s.EmailAddress)
            .HasDatabaseName("idx_suppression_email");
    }
}
