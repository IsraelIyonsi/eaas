using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class DnsRecordConfiguration : IEntityTypeConfiguration<DnsRecord>
{
    public void Configure(EntityTypeBuilder<DnsRecord> builder)
    {
        builder.ToTable("dns_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.DomainId)
            .HasColumnName("domain_id")
            .IsRequired();

        builder.Property(r => r.RecordType)
            .HasColumnName("record_type")
            .IsRequired();

        builder.Property(r => r.RecordName)
            .HasColumnName("record_name")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(r => r.RecordValue)
            .HasColumnName("record_value")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(r => r.Purpose)
            .HasColumnName("purpose");

        builder.Property(r => r.IsVerified)
            .HasColumnName("is_verified")
            .HasDefaultValue(false);

        builder.Property(r => r.VerifiedAt)
            .HasColumnName("verified_at");

        builder.Property(r => r.ActualValue)
            .HasColumnName("actual_value")
            .HasMaxLength(1024);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Index
        builder.HasIndex(r => r.DomainId)
            .HasDatabaseName("idx_dns_records_domain");
    }
}
