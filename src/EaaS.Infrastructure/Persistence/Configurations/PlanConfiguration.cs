using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Tier)
            .HasColumnName("tier")
            .HasDefaultValue(PlanTier.Free);

        builder.Property(p => p.MonthlyPriceUsd)
            .HasColumnName("monthly_price_usd")
            .HasColumnType("decimal(10,2)")
            .HasDefaultValue(0m);

        builder.Property(p => p.AnnualPriceUsd)
            .HasColumnName("annual_price_usd")
            .HasColumnType("decimal(10,2)")
            .HasDefaultValue(0m);

        builder.Property(p => p.DailyEmailLimit)
            .HasColumnName("daily_email_limit")
            .HasDefaultValue(100);

        builder.Property(p => p.MonthlyEmailLimit)
            .HasColumnName("monthly_email_limit")
            .HasDefaultValue(3000L);

        builder.Property(p => p.MaxApiKeys)
            .HasColumnName("max_api_keys")
            .HasDefaultValue(3);

        builder.Property(p => p.MaxDomains)
            .HasColumnName("max_domains")
            .HasDefaultValue(2);

        builder.Property(p => p.MaxTemplates)
            .HasColumnName("max_templates")
            .HasDefaultValue(10);

        builder.Property(p => p.MaxWebhooks)
            .HasColumnName("max_webhooks")
            .HasDefaultValue(5);

        builder.Property(p => p.CustomDomainBranding)
            .HasColumnName("custom_domain_branding")
            .HasDefaultValue(false);

        builder.Property(p => p.PrioritySupport)
            .HasColumnName("priority_support")
            .HasDefaultValue(false);

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("uq_plans_name");

        builder.HasIndex(p => new { p.Tier, p.IsActive })
            .HasDatabaseName("idx_plans_tier_active");

        builder.HasMany(p => p.Subscriptions)
            .WithOne(s => s.Plan)
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
