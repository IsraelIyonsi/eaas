using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Domains;

public sealed class ListDomainsHandler : IRequestHandler<ListDomainsQuery, IReadOnlyList<DomainSummary>>
{
    private readonly AppDbContext _dbContext;

    public ListDomainsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DomainSummary>> Handle(ListDomainsQuery request, CancellationToken cancellationToken)
    {
        var domains = await _dbContext.Domains
            .AsNoTracking()
            .Include(d => d.DnsRecords)
            .Where(d => d.TenantId == request.TenantId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return domains.Select(d => new DomainSummary(
            d.Id,
            d.DomainName,
            d.Status.ToString(),
            d.DnsRecords.Select(r => new DnsRecordDto(
                r.Id, r.RecordType, r.RecordName, r.RecordValue,
                r.Purpose.ToString().ToLowerInvariant(), r.IsVerified)).ToList(),
            d.CreatedAt,
            d.VerifiedAt,
            d.LastCheckedAt)).ToList();
    }
}
