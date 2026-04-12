using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Domains;

public sealed class GetDomainHandler : IRequestHandler<GetDomainQuery, DomainSummary>
{
    private readonly AppDbContext _dbContext;

    public GetDomainHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DomainSummary> Handle(GetDomainQuery request, CancellationToken cancellationToken)
    {
        var domain = await _dbContext.Domains
            .AsNoTracking()
            .Include(d => d.DnsRecords)
            .Where(d => d.Id == request.Id && d.TenantId == request.TenantId && d.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Domain with id '{request.Id}' not found.");

        return new DomainSummary(
            domain.Id,
            domain.DomainName,
            domain.Status.ToString(),
            domain.DnsRecords.Select(r => new DnsRecordDto(
                r.Id,
                r.RecordType.ToString().ToUpperInvariant(),
                r.RecordName,
                r.RecordValue,
                r.Purpose.ToString().ToLowerInvariant(),
                r.IsVerified)).ToList(),
            domain.CreatedAt,
            domain.VerifiedAt,
            domain.LastCheckedAt);
    }
}
