using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class SendingDomainConfiguration : IEntityTypeConfiguration<SendingDomain>
{
    public void Configure(EntityTypeBuilder<SendingDomain> builder)
    {
        builder.ToTable("domains");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(d => d.DomainName)
            .HasColumnName("domain_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasDefaultValue(DomainStatus.PendingVerification);

        builder.Property(d => d.SesIdentityArn)
            .HasColumnName("ses_identity_arn")
            .HasMaxLength(512);

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(d => d.VerifiedAt)
            .HasColumnName("verified_at");

        builder.Property(d => d.LastCheckedAt)
            .HasColumnName("last_checked_at");

        builder.Property(d => d.DeletedAt)
            .HasColumnName("deleted_at");

        // Unique constraint on (tenant_id, domain_name)
        builder.HasIndex(d => new { d.TenantId, d.DomainName })
            .IsUnique()
            .HasDatabaseName("uq_domains_tenant_name");

        // Indexes
        builder.HasIndex(d => d.TenantId)
            .HasDatabaseName("idx_domains_tenant");

        builder.HasIndex(d => new { d.TenantId, d.Status })
            .HasDatabaseName("idx_domains_status");

        builder.HasMany(d => d.DnsRecords)
            .WithOne(r => r.Domain)
            .HasForeignKey(r => r.DomainId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
