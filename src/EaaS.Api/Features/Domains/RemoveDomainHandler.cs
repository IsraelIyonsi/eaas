using EaaS.Domain.Exceptions;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.Domains;

public sealed partial class RemoveDomainHandler : IRequestHandler<RemoveDomainCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly IDomainIdentityService _emailDeliveryService;
    private readonly ILogger<RemoveDomainHandler> _logger;

    public RemoveDomainHandler(AppDbContext dbContext, IDomainIdentityService emailDeliveryService, ILogger<RemoveDomainHandler> logger)
    {
        _dbContext = dbContext;
        _emailDeliveryService = emailDeliveryService;
        _logger = logger;
    }

    public async Task Handle(RemoveDomainCommand request, CancellationToken cancellationToken)
    {
        var domain = await _dbContext.Domains
            .Where(d => d.Id == request.Id && d.TenantId == request.TenantId && d.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Domain with id '{request.Id}' not found.");

        // Check for pending emails using this domain
        var domainName = domain.DomainName;
        var hasPendingEmails = await _dbContext.Emails
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == request.TenantId
                           && e.FromEmail.EndsWith("@" + domainName)
                           && (e.Status == EmailStatus.Queued || e.Status == EmailStatus.Sending),
                cancellationToken);

        if (hasPendingEmails)
            throw new ConflictException($"Cannot remove domain '{domainName}' while emails are in Queued or Sending status.");

        // Soft delete
        domain.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Optionally remove from SES
        await _emailDeliveryService.DeleteDomainIdentityAsync(domainName, cancellationToken);

        LogDomainRemoved(_logger, request.Id, domainName, request.TenantId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Domain removed: DomainId={DomainId}, DomainName={DomainName}, TenantId={TenantId}")]
    private static partial void LogDomainRemoved(ILogger logger, Guid domainId, string domainName, Guid tenantId);
}
