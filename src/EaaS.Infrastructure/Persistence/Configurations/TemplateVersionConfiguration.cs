using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("template_versions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(v => v.TemplateId)
            .HasColumnName("template_id")
            .IsRequired();

        builder.Property(v => v.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(v => v.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.Subject)
            .HasColumnName("subject")
            .HasColumnType("text")
            .IsRequired();

        // NOTE (MED-6): see TemplateConfiguration — CLR + DB names kept as HtmlBody/TextBody.
        builder.Property(v => v.HtmlBody)
            .HasColumnName("html_body")
            .HasColumnType("text");

        builder.Property(v => v.TextBody)
            .HasColumnName("text_body")
            .HasColumnType("text");

        builder.Property(v => v.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // FK: TemplateId -> templates
        builder.HasOne(v => v.Template)
            .WithMany(t => t.Versions)
            .HasForeignKey(v => v.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(v => new { v.TemplateId, v.Version })
            .IsDescending(false, true)
            .HasDatabaseName("ix_template_versions_template_version");

        builder.HasIndex(v => new { v.TemplateId, v.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_template_versions_template_created");
    }
}
