using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class UpdateTemplateHandler : IRequestHandler<UpdateTemplateCommand, TemplateResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;

    public UpdateTemplateHandler(AppDbContext dbContext, ICacheService cacheService)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
    }

    public async Task<TemplateResult> Handle(UpdateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.Templates
            .Where(t => t.TenantId == request.TenantId
                        && t.Id == request.TemplateId
                        && t.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
            throw new KeyNotFoundException($"Template with ID '{request.TemplateId}' not found.");

        // Check name uniqueness if changing
        if (request.Name is not null && request.Name != template.Name)
        {
            var nameExists = await _dbContext.Templates
                .AsNoTracking()
                .AnyAsync(t => t.TenantId == request.TenantId
                               && t.Name == request.Name
                               && t.Id != template.Id
                               && t.DeletedAt == null, cancellationToken);

            if (nameExists)
                throw new InvalidOperationException($"Template with name '{request.Name}' already exists.");

            template.Name = request.Name;
        }

        if (request.SubjectTemplate is not null)
            template.SubjectTemplate = request.SubjectTemplate;

        if (request.HtmlBody is not null)
            template.HtmlBody = request.HtmlBody;

        if (request.TextBody is not null)
            template.TextBody = request.TextBody;

        template.Version++;
        template.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate template cache
        await _cacheService.InvalidateTemplateCacheAsync(template.Id, cancellationToken);

        return new TemplateResult(
            template.Id,
            template.Name,
            template.SubjectTemplate,
            template.HtmlBody,
            template.TextBody,
            template.Version,
            template.CreatedAt,
            template.UpdatedAt);
    }
}
