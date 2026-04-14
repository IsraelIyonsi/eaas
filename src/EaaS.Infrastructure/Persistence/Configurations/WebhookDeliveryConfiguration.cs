using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Idempotency table config (H11). The unique index on
/// <c>(webhook_id, email_id, event_type)</c> is the dedup key used by
/// <see cref="EaaS.Infrastructure.Messaging.WebhookDispatchConsumer"/>.
/// </summary>
public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.WebhookId)
            .HasColumnName("webhook_id")
            .IsRequired();

        builder.Property(d => d.EmailId)
            .HasColumnName("email_id")
            .IsRequired();

        builder.Property(d => d.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(d => d.FirstAttemptAt)
            .HasColumnName("first_attempt_at")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(d => d.LastAttemptAt)
            .HasColumnName("last_attempt_at")
            .IsRequired();

        builder.Property(d => d.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(d => d.ResponseStatusCode)
            .HasColumnName("response_status_code");

        builder.Property(d => d.ResponseBodySnippet)
            .HasColumnName("response_body_snippet")
            .HasMaxLength(1024);

        builder.HasOne(d => d.Webhook)
            .WithMany()
            .HasForeignKey(d => d.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dedup key: one row per (webhook, email, event_type) tuple.
        builder.HasIndex(d => new { d.WebhookId, d.EmailId, d.EventType })
            .IsUnique()
            .HasDatabaseName("ux_webhook_deliveries_dedup");
    }
}
