using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.Inbound.Emails;

public sealed partial class DeleteInboundEmailHandler
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DeleteInboundEmailHandler> _logger;

    public DeleteInboundEmailHandler(AppDbContext dbContext, ILogger<DeleteInboundEmailHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        var email = await _dbContext.InboundEmails
            .Where(e => e.Id == id && e.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Inbound email with id '{id}' not found.");

        _dbContext.InboundEmails.Remove(email);
        await _dbContext.SaveChangesAsync(cancellationToken);

        LogDeleted(_logger, id, tenantId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Inbound email deleted: EmailId={EmailId}, TenantId={TenantId}")]
    private static partial void LogDeleted(ILogger logger, Guid emailId, Guid tenantId);
}
