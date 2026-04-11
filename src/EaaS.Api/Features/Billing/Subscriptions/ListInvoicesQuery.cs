using MediatR;

namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed record ListInvoicesQuery(
    Guid TenantId,
    int Page = 1,
    int PageSize = 20) : IRequest<InvoiceListResult>;
