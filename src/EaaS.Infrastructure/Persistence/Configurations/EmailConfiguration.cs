using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class EmailConfiguration : IEntityTypeConfiguration<Email>
{
    public void Configure(EntityTypeBuilder<Email> builder)
    {
        builder.ToTable("emails");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.ApiKeyId)
            .HasColumnName("api_key_id")
            .IsRequired();

        builder.Property(e => e.MessageId)
            .HasColumnName("message_id")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.BatchId)
            .HasColumnName("batch_id")
            .HasMaxLength(32);

        builder.Property(e => e.FromEmail)
            .HasColumnName("from_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(e => e.FromName)
            .HasColumnName("from_name")
            .HasMaxLength(255);

        builder.Property(e => e.ToEmails)
            .HasColumnName("to_emails")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.CcEmails)
            .HasColumnName("cc_emails")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(e => e.BccEmails)
            .HasColumnName("bcc_emails")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(e => e.Subject)
            .HasColumnName("subject")
            .HasMaxLength(998)
            .IsRequired();

        builder.Property(e => e.HtmlBody)
            .HasColumnName("html_body")
            .HasColumnType("text");

        builder.Property(e => e.TextBody)
            .HasColumnName("text_body")
            .HasColumnType("text");

        builder.Property(e => e.TemplateId)
            .HasColumnName("template_id");

        builder.Property(e => e.Variables)
            .HasColumnName("variables")
            .HasColumnType("jsonb");

        builder.Property(e => e.Attachments)
            .HasColumnName("attachments")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(e => e.Tags)
            .HasColumnName("tags")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'");

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(e => e.TrackOpens)
            .HasColumnName("track_opens")
            .HasDefaultValue(true);

        builder.Property(e => e.TrackClicks)
            .HasColumnName("track_clicks")
            .HasDefaultValue(true);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasDefaultValue(EmailStatus.Queued);

        builder.Property(e => e.SesMessageId)
            .HasColumnName("ses_message_id")
            .HasMaxLength(255);

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.SentAt)
            .HasColumnName("sent_at");

        builder.Property(e => e.DeliveredAt)
            .HasColumnName("delivered_at");

        builder.Property(e => e.OpenedAt)
            .HasColumnName("opened_at");

        builder.Property(e => e.TrackingId)
            .HasColumnName("tracking_id")
            .HasMaxLength(64);

        builder.Property(e => e.ClickedAt)
            .HasColumnName("clicked_at");

        // Indexes
        builder.HasIndex(e => e.MessageId)
            .IsUnique()
            .HasDatabaseName("idx_emails_message_id");

        builder.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("idx_emails_tenant_status");

        builder.HasIndex(e => new { e.TenantId, e.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("idx_emails_tenant_created");

        builder.HasIndex(e => e.BatchId)
            .HasFilter("batch_id IS NOT NULL")
            .HasDatabaseName("idx_emails_batch");

        builder.HasIndex(e => e.TemplateId)
            .HasFilter("template_id IS NOT NULL")
            .HasDatabaseName("idx_emails_template");

        builder.HasIndex(e => e.ApiKeyId)
            .HasDatabaseName("idx_emails_api_key");

        builder.HasIndex(e => e.FromEmail)
            .HasDatabaseName("idx_emails_from");

        builder.HasMany(e => e.Events)
            .WithOne(ev => ev.Email)
            .HasForeignKey(ev => ev.EmailId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
