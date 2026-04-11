using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed class GetTenantHandler : IRequestHandler<GetTenantQuery, TenantDetailResult>
{
    private readonly AppDbContext _dbContext;

    public GetTenantHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantDetailResult> Handle(GetTenantQuery request, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == request.TenantId)
            .Select(t => new TenantDetailResult(
                t.Id,
                t.Name,
                t.Status.ToString().ToLowerInvariant(),
                t.CompanyName,
                t.ContactEmail,
                t.MaxApiKeys,
                t.MaxDomainsCount,
                t.MonthlyEmailLimit,
                t.Notes,
                t.ApiKeys.Count,
                t.Domains.Count,
                t.Emails.Count,
                t.CreatedAt,
                t.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
            throw new NotFoundException("Tenant not found");

        return result;
    }
}
