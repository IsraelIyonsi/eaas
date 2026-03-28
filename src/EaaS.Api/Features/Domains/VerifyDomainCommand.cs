using MediatR;

namespace EaaS.Api.Features.Domains;

public sealed record VerifyDomainCommand(Guid Id, Guid TenantId) : IRequest<VerifyDomainResult>;

public sealed record VerifyDomainResult(
    Guid Id,
    string DomainName,
    string Status,
    IReadOnlyList<DnsRecordDto> DnsRecords,
    DateTime? VerifiedAt);
