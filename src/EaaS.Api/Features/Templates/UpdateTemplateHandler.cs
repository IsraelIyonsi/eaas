using EaaS.Domain.Entities;
using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class UpdateTemplateHandler : IRequestHandler<UpdateTemplateCommand, TemplateResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ITemplateCache _templateCache;

    public UpdateTemplateHandler(AppDbContext dbContext, ITemplateCache templateCache)
    {
        _dbContext = dbContext;
        _templateCache = templateCache;
    }

    public async Task<TemplateResult> Handle(UpdateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.Templates
            .Where(t => t.TenantId == request.TenantId
                        && t.Id == request.TemplateId
                        && t.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
            throw new NotFoundException($"Template with ID '{request.TemplateId}' not found.");

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
                throw new ConflictException($"Template with name '{request.Name}' already exists.");
        }

        // Snapshot current state before applying update.
        // MED-6: public contract uses HtmlTemplate/TextTemplate; entity stores HtmlBody/TextBody.
        var snapshot = new TemplateVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            Version = template.Version,
            Name = template.Name,
            Subject = template.SubjectTemplate,
            HtmlBody = template.HtmlBody,
            TextBody = template.TextBody,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.TemplateVersions.Add(snapshot);

        // Apply updates
        if (request.Name is not null)
            template.Name = request.Name;

        if (request.SubjectTemplate is not null)
            template.SubjectTemplate = request.SubjectTemplate;

        if (request.HtmlTemplate is not null)
            template.HtmlBody = request.HtmlTemplate;

        if (request.TextTemplate is not null)
            template.TextBody = request.TextTemplate;

        template.Version++;
        template.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate template cache
        await _templateCache.InvalidateTemplateCacheAsync(template.Id, cancellationToken);

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
