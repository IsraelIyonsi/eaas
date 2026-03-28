using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.SubjectTemplate)
            .HasColumnName("subject_template")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(t => t.HtmlBody)
            .HasColumnName("html_body")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(t => t.TextBody)
            .HasColumnName("text_body")
            .HasColumnType("text");

        builder.Property(t => t.VariablesSchema)
            .HasColumnName("variables_schema")
            .HasColumnType("jsonb");

        builder.Property(t => t.Version)
            .HasColumnName("version")
            .HasDefaultValue(1);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        // Partial unique index: name unique among non-deleted templates per tenant
        builder.HasIndex(t => new { t.TenantId, t.Name })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("uq_templates_tenant_name_active");

        // Indexes
        builder.HasIndex(t => t.TenantId)
            .HasDatabaseName("idx_templates_tenant");

        builder.HasIndex(t => new { t.TenantId, t.DeletedAt })
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("idx_templates_deleted");
    }
}
