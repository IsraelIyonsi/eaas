using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed record ListTenantsQuery(
    int Page,
    int PageSize,
    string? Status,
    string? Search) : IRequest<PagedResponse<TenantSummaryResult>>;
