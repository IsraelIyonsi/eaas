using MediatR;

namespace EaaS.Api.Features.Domains;

public sealed record ListDomainsQuery(Guid TenantId) : IRequest<IReadOnlyList<DomainSummary>>;

public sealed record DomainSummary(
    Guid Id,
    string DomainName,
    string Status,
    IReadOnlyList<DnsRecordDto> DnsRecords,
    DateTime CreatedAt,
    DateTime? VerifiedAt,
    DateTime? LastCheckedAt);
