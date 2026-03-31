using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class DeleteTemplateHandler : IRequestHandler<DeleteTemplateCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;

    public DeleteTemplateHandler(AppDbContext dbContext, ICacheService cacheService)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
    }

    public async Task Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.Templates
            .Where(t => t.TenantId == request.TenantId
                        && t.Id == request.TemplateId
                        && t.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
            throw new EaaS.Domain.Exceptions.NotFoundException($"Template with ID '{request.TemplateId}' not found.");

        // Soft delete
        template.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cacheService.InvalidateTemplateCacheAsync(template.Id, cancellationToken);
    }
}
