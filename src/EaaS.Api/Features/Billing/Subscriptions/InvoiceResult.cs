namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed record InvoiceResult(
    Guid Id,
    Guid SubscriptionId,
    string InvoiceNumber,
    decimal AmountUsd,
    string Currency,
    string Status,
    string Provider,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime? PaidAt,
    DateTime CreatedAt);

public sealed record InvoiceListResult(
    List<InvoiceResult> Items,
    int Page,
    int PageSize,
    int TotalCount);
