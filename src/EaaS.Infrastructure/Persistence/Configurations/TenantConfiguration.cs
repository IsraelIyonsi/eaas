using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasDefaultValue(TenantStatus.Active);

        builder.Property(t => t.ContactEmail)
            .HasColumnName("contact_email")
            .HasMaxLength(255);

        builder.Property(t => t.CompanyName)
            .HasColumnName("company_name")
            .HasMaxLength(255);

        builder.Property(t => t.LegalEntityName)
            .HasColumnName("legal_entity_name")
            .HasMaxLength(255);

        builder.Property(t => t.PostalAddress)
            .HasColumnName("postal_address")
            .HasColumnType("text");

        builder.Property(t => t.MaxApiKeys)
            .HasColumnName("max_api_keys");

        builder.Property(t => t.MaxDomainsCount)
            .HasColumnName("max_domains_count");

        builder.Property(t => t.MonthlyEmailLimit)
            .HasColumnName("monthly_email_limit");

        builder.Property(t => t.PasswordHash)
            .HasColumnName("password_hash");

        builder.Property(t => t.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasMany(t => t.ApiKeys)
            .WithOne(a => a.Tenant)
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Domains)
            .WithOne(d => d.Tenant)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Templates)
            .WithOne(t2 => t2.Tenant)
            .HasForeignKey(t2 => t2.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Emails)
            .WithOne(e => e.Tenant)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.SuppressionEntries)
            .WithOne(s => s.Tenant)
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Webhooks)
            .WithOne(w => w.Tenant)
            .HasForeignKey(w => w.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed default tenant for Sprint 1
        builder.HasData(new Tenant
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Default",
            CreatedAt = new DateTime(2026, 3, 27, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 3, 27, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
