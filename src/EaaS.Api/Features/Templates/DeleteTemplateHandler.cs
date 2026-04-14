using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.Templates;

public sealed partial class DeleteTemplateHandler : IRequestHandler<DeleteTemplateCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly ITemplateCache _templateCache;
    private readonly ILogger<DeleteTemplateHandler> _logger;

    public DeleteTemplateHandler(
        AppDbContext dbContext,
        ITemplateCache templateCache,
        ILogger<DeleteTemplateHandler> logger)
    {
        _dbContext = dbContext;
        _templateCache = templateCache;
        _logger = logger;
    }

    public async Task Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        LogDeleteStarted(_logger, request.TenantId, request.TemplateId);

        var template = await _dbContext.Templates
            .Where(t => t.TenantId == request.TenantId
                        && t.Id == request.TemplateId
                        && t.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
        {
            LogTemplateNotFound(_logger, request.TenantId, request.TemplateId);
            throw new NotFoundException($"Template with ID '{request.TemplateId}' not found.");
        }

        // Soft delete — set tombstone, do NOT cascade into template_versions
        // (audit history must be preserved).
        template.DeletedAt = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Log infrastructure faults with full context so prod issues
            // (concurrency, connection drop, FK constraint edge cases) are
            // diagnosable rather than being lost in a generic 500 path.
            // GlobalExceptionHandler catches DbUpdateException via the `_`
            // fallback and returns a 500 JSON body before the response stream
            // is aborted — which is what prevents the nginx 502 surface.
            LogDeleteDbFailure(_logger, ex, request.TenantId, request.TemplateId);
            throw;
        }

        // Cache invalidation MUST NOT fail the request — Redis going down must
        // not turn a successful soft-delete into a 502 to the user. The cache
        // implementation already swallows its own exceptions, but we belt-and-
        // brace here in case a new cache impl is wired up that doesn't.
        try
        {
            await _templateCache.InvalidateTemplateCacheAsync(template.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCacheInvalidateFailed(_logger, ex, template.Id);
        }

        LogDeleteCompleted(_logger, request.TenantId, template.Id);
    }

    [LoggerMessage(EventId = 4100, Level = LogLevel.Information,
        Message = "Template soft-delete requested TenantId={TenantId} TemplateId={TemplateId}")]
    private static partial void LogDeleteStarted(ILogger logger, Guid tenantId, Guid templateId);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Information,
        Message = "Template soft-delete completed TenantId={TenantId} TemplateId={TemplateId}")]
    private static partial void LogDeleteCompleted(ILogger logger, Guid tenantId, Guid templateId);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Information,
        Message = "Template soft-delete target not found TenantId={TenantId} TemplateId={TemplateId}")]
    private static partial void LogTemplateNotFound(ILogger logger, Guid tenantId, Guid templateId);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Error,
        Message = "Template soft-delete DB write failed TenantId={TenantId} TemplateId={TemplateId}")]
    private static partial void LogDeleteDbFailure(ILogger logger, Exception ex, Guid tenantId, Guid templateId);

    [LoggerMessage(EventId = 4104, Level = LogLevel.Warning,
        Message = "Template cache invalidation failed after successful soft-delete TemplateId={TemplateId}")]
    private static partial void LogCacheInvalidateFailed(ILogger logger, Exception ex, Guid templateId);
}
