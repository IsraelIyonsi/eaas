using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Domains;

public sealed class RemoveDomainHandler : IRequestHandler<RemoveDomainCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailDeliveryService _emailDeliveryService;

    public RemoveDomainHandler(AppDbContext dbContext, IEmailDeliveryService emailDeliveryService)
    {
        _dbContext = dbContext;
        _emailDeliveryService = emailDeliveryService;
    }

    public async Task Handle(RemoveDomainCommand request, CancellationToken cancellationToken)
    {
        var domain = await _dbContext.Domains
            .Where(d => d.Id == request.Id && d.TenantId == request.TenantId && d.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EaaS.Domain.Exceptions.NotFoundException($"Domain with id '{request.Id}' not found.");

        // Check for pending emails using this domain
        var domainName = domain.DomainName;
        var hasPendingEmails = await _dbContext.Emails
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == request.TenantId
                           && e.FromEmail.EndsWith("@" + domainName)
                           && (e.Status == EmailStatus.Queued || e.Status == EmailStatus.Sending),
                cancellationToken);

        if (hasPendingEmails)
            throw new InvalidOperationException($"Cannot remove domain '{domainName}' while emails are in Queued or Sending status.");

        // Soft delete
        domain.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Optionally remove from SES
        await _emailDeliveryService.DeleteDomainIdentityAsync(domainName, cancellationToken);
    }
}
