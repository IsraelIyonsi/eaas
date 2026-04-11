using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Analytics;

public sealed class GetPlatformSummaryHandler : IRequestHandler<GetPlatformSummaryQuery, PlatformSummaryResult>
{
    private readonly AppDbContext _dbContext;

    public GetPlatformSummaryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlatformSummaryResult> Handle(GetPlatformSummaryQuery request, CancellationToken cancellationToken)
    {
        var totalTenants = await _dbContext.Tenants.CountAsync(cancellationToken);
        var activeTenants = await _dbContext.Tenants.CountAsync(t => t.Status == TenantStatus.Active, cancellationToken);
        var totalEmails = await _dbContext.Emails.CountAsync(cancellationToken);
        var totalDomains = await _dbContext.Domains.CountAsync(cancellationToken);
        var totalApiKeys = await _dbContext.ApiKeys.CountAsync(cancellationToken);

        return new PlatformSummaryResult(totalTenants, activeTenants, totalEmails, totalDomains, totalApiKeys);
    }
}
