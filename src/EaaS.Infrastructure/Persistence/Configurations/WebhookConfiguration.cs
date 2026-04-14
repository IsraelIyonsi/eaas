using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> builder)
    {
        builder.ToTable("webhooks");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(w => w.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(w => w.Url)
            .HasColumnName("url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(w => w.Events)
            .HasColumnName("events")
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(w => w.Secret)
            .HasColumnName("secret")
            .HasMaxLength(255);

        builder.Property(w => w.Status)
            .HasColumnName("status")
            .HasDefaultValue(WebhookStatus.Active);

        builder.Property(w => w.ConsecutiveFailures)
            .HasColumnName("consecutive_failures")
            .HasDefaultValue(0);

        builder.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(w => w.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Index
        builder.HasIndex(w => w.TenantId)
            .HasDatabaseName("idx_webhooks_tenant");
    }
}
