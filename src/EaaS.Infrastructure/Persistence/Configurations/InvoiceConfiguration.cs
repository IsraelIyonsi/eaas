using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.SubscriptionId)
            .HasColumnName("subscription_id")
            .IsRequired();

        builder.Property(i => i.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(i => i.InvoiceNumber)
            .HasColumnName("invoice_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.AmountUsd)
            .HasColumnName("amount_usd")
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(i => i.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasDefaultValue(InvoiceStatus.Pending);

        builder.Property(i => i.Provider)
            .HasColumnName("provider");

        builder.Property(i => i.ExternalInvoiceId)
            .HasColumnName("external_invoice_id");

        builder.Property(i => i.ExternalPaymentId)
            .HasColumnName("external_payment_id");

        builder.Property(i => i.PaymentMethod)
            .HasColumnName("payment_method");

        builder.Property(i => i.PeriodStart)
            .HasColumnName("period_start")
            .IsRequired();

        builder.Property(i => i.PeriodEnd)
            .HasColumnName("period_end")
            .IsRequired();

        builder.Property(i => i.PaidAt)
            .HasColumnName("paid_at");

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(i => i.Subscription)
            .WithMany(s => s.Invoices)
            .HasForeignKey(i => i.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Tenant)
            .WithMany(t => t.Invoices)
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique()
            .HasDatabaseName("uq_invoices_invoice_number");

        builder.HasIndex(i => new { i.TenantId, i.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("idx_invoices_tenant_created");
    }
}
