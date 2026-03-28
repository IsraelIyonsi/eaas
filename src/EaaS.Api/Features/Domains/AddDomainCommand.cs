using MediatR;

namespace EaaS.Api.Features.Domains;

public sealed record AddDomainCommand(string DomainName, Guid TenantId) : IRequest<AddDomainResult>;

public sealed record AddDomainResult(
    Guid Id,
    string DomainName,
    string Status,
    IReadOnlyList<DnsRecordDto> DnsRecords,
    DateTime CreatedAt);

public sealed record DnsRecordDto(
    Guid Id,
    string Type,
    string Name,
    string Value,
    string Purpose,
    bool IsVerified);
