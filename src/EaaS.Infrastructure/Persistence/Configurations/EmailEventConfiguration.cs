using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class EmailEventConfiguration : IEntityTypeConfiguration<EmailEvent>
{
    public void Configure(EntityTypeBuilder<EmailEvent> builder)
    {
        builder.ToTable("email_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.EmailId)
            .HasColumnName("email_id")
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type");

        builder.Property(e => e.Data)
            .HasColumnName("data")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(e => e.EmailId)
            .HasDatabaseName("idx_email_events_email");

        builder.HasIndex(e => new { e.EventType, e.CreatedAt })
            .HasDatabaseName("idx_email_events_type");
    }
}
