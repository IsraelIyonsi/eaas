using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal AmountUsd { get; set; }
    public string Currency { get; set; } = "USD";
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public PaymentProvider Provider { get; set; }
    public string? ExternalInvoiceId { get; set; }
    public string? ExternalPaymentId { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Subscription Subscription { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
