using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class WebhookDeliveryLogConfiguration : IEntityTypeConfiguration<WebhookDeliveryLog>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryLog> builder)
    {
        builder.ToTable("webhook_delivery_logs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.WebhookId)
            .HasColumnName("webhook_id")
            .IsRequired();

        builder.Property(l => l.EmailId)
            .HasColumnName("email_id")
            .IsRequired();

        builder.Property(l => l.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.StatusCode)
            .HasColumnName("status_code");

        builder.Property(l => l.Success)
            .HasColumnName("success");

        builder.Property(l => l.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(l => l.AttemptNumber)
            .HasColumnName("attempt_number");

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(l => l.WebhookId)
            .HasDatabaseName("idx_webhook_delivery_logs_webhook");

        builder.HasIndex(l => l.EmailId)
            .HasDatabaseName("idx_webhook_delivery_logs_email");

        builder.HasIndex(l => l.CreatedAt)
            .HasDatabaseName("idx_webhook_delivery_logs_created");
    }
}
