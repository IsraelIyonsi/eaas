using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class DeleteTemplateHandler : IRequestHandler<DeleteTemplateCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly ITemplateCache _templateCache;

    public DeleteTemplateHandler(AppDbContext dbContext, ITemplateCache templateCache)
    {
        _dbContext = dbContext;
        _templateCache = templateCache;
    }

    public async Task Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.Templates
            .Where(t => t.TenantId == request.TenantId
                        && t.Id == request.TemplateId
                        && t.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
            throw new NotFoundException($"Template with ID '{request.TemplateId}' not found.");

        // Soft delete
        template.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _templateCache.InvalidateTemplateCacheAsync(template.Id, cancellationToken);
    }
}
