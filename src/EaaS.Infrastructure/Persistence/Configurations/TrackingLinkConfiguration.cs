using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class TrackingLinkConfiguration : IEntityTypeConfiguration<TrackingLink>
{
    public void Configure(EntityTypeBuilder<TrackingLink> builder)
    {
        builder.ToTable("tracking_links");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.EmailId)
            .HasColumnName("email_id")
            .IsRequired();

        builder.Property(t => t.Token)
            .HasColumnName("token")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(t => t.OriginalUrl)
            .HasColumnName("original_url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(t => t.ClickedAt)
            .HasColumnName("clicked_at");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("idx_tracking_links_token");

        builder.HasIndex(t => t.EmailId)
            .HasDatabaseName("idx_tracking_links_email");

        builder.HasOne(t => t.Email)
            .WithMany()
            .HasForeignKey(t => t.EmailId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
