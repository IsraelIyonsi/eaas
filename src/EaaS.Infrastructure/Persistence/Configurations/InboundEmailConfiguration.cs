using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class InboundEmailConfiguration : IEntityTypeConfiguration<InboundEmail>
{
    public void Configure(EntityTypeBuilder<InboundEmail> builder)
    {
        builder.ToTable("inbound_emails");

        builder.HasKey(e => new { e.Id, e.ReceivedAt });

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.MessageId)
            .HasColumnName("message_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.FromEmail)
            .HasColumnName("from_email")
            .HasMaxLength(255)
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

        builder.Property(e => e.ReplyTo)
            .HasColumnName("reply_to")
            .HasMaxLength(255);

        builder.Property(e => e.Subject)
            .HasColumnName("subject")
            .HasMaxLength(1024);

        builder.Property(e => e.HtmlBody)
            .HasColumnName("html_body")
            .HasColumnType("text");

        builder.Property(e => e.TextBody)
            .HasColumnName("text_body")
            .HasColumnType("text");

        builder.Property(e => e.Headers)
            .HasColumnName("headers")
            .HasColumnType("jsonb");

        builder.Property(e => e.Tags)
            .HasColumnName("tags")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'");

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasDefaultValue(InboundEmailStatus.Received);

        builder.Property(e => e.S3Key)
            .HasColumnName("s3_key")
            .HasMaxLength(512);

        builder.Property(e => e.SpamScore)
            .HasColumnName("spam_score")
            .HasColumnType("decimal(5,2)");

        builder.Property(e => e.SpamVerdict)
            .HasColumnName("spam_verdict")
            .HasMaxLength(20);

        builder.Property(e => e.VirusVerdict)
            .HasColumnName("virus_verdict")
            .HasMaxLength(20);

        builder.Property(e => e.SpfVerdict)
            .HasColumnName("spf_verdict")
            .HasMaxLength(20);

        builder.Property(e => e.DkimVerdict)
            .HasColumnName("dkim_verdict")
            .HasMaxLength(20);

        builder.Property(e => e.DmarcVerdict)
            .HasColumnName("dmarc_verdict")
            .HasMaxLength(20);

        builder.Property(e => e.InReplyTo)
            .HasColumnName("in_reply_to")
            .HasMaxLength(255);

        builder.Property(e => e.References)
            .HasColumnName("references")
            .HasColumnType("text");

        builder.Property(e => e.OutboundEmailId)
            .HasColumnName("outbound_email_id");

        builder.Property(e => e.ReceivedAt)
            .HasColumnName("received_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("idx_inbound_emails_tenant");

        builder.HasIndex(e => new { e.TenantId, e.ReceivedAt })
            .IsDescending(false, true)
            .HasDatabaseName("idx_inbound_emails_tenant_received");

        builder.HasIndex(e => new { e.TenantId, e.FromEmail })
            .HasDatabaseName("idx_inbound_emails_from");

        builder.HasIndex(e => e.MessageId)
            .HasDatabaseName("idx_inbound_emails_message_id");

        builder.HasIndex(e => e.InReplyTo)
            .HasFilter("in_reply_to IS NOT NULL")
            .HasDatabaseName("idx_inbound_emails_in_reply_to");

        builder.HasIndex(e => e.OutboundEmailId)
            .HasFilter("outbound_email_id IS NOT NULL")
            .HasDatabaseName("idx_inbound_emails_outbound");

        // Relationships
        builder.HasOne(e => e.OutboundEmail)
            .WithMany()
            .HasForeignKey(e => e.OutboundEmailId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Attachments)
            .WithOne(a => a.InboundEmail)
            .HasForeignKey(a => a.InboundEmailId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
