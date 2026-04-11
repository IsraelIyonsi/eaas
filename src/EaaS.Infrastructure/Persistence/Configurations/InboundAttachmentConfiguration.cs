using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class InboundAttachmentConfiguration : IEntityTypeConfiguration<InboundAttachment>
{
    public void Configure(EntityTypeBuilder<InboundAttachment> builder)
    {
        builder.ToTable("inbound_attachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.InboundEmailId)
            .HasColumnName("inbound_email_id")
            .IsRequired();

        builder.Property(a => a.Filename)
            .HasColumnName("filename")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(a => a.S3Key)
            .HasColumnName("s3_key")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(a => a.ContentId)
            .HasColumnName("content_id")
            .HasMaxLength(255);

        builder.Property(a => a.IsInline)
            .HasColumnName("is_inline")
            .HasDefaultValue(false);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(a => a.InboundEmailId)
            .HasDatabaseName("idx_inbound_attachments_email");
    }
}
