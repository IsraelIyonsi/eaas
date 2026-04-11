using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class InboundRuleConfiguration : IEntityTypeConfiguration<InboundRule>
{
    public void Configure(EntityTypeBuilder<InboundRule> builder)
    {
        builder.ToTable("inbound_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(r => r.DomainId)
            .HasColumnName("domain_id")
            .IsRequired();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.MatchPattern)
            .HasColumnName("match_pattern")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.Action)
            .HasColumnName("action")
            .HasDefaultValue(InboundRuleAction.Store);

        builder.Property(r => r.WebhookUrl)
            .HasColumnName("webhook_url")
            .HasMaxLength(2048);

        builder.Property(r => r.ForwardTo)
            .HasColumnName("forward_to")
            .HasMaxLength(255);

        builder.Property(r => r.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(r => r.Priority)
            .HasColumnName("priority")
            .HasDefaultValue(0);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("idx_inbound_rules_tenant");

        builder.HasIndex(r => r.DomainId)
            .HasDatabaseName("idx_inbound_rules_domain");

        // Relationships
        builder.HasOne(r => r.Domain)
            .WithMany()
            .HasForeignKey(r => r.DomainId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
