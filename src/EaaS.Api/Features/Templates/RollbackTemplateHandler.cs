using EaaS.Domain.Entities;
using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class RollbackTemplateHandler : IRequestHandler<RollbackTemplateCommand, TemplateResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ITemplateCache _templateCache;

    public RollbackTemplateHandler(AppDbContext dbContext, ITemplateCache templateCache)
    {
        _dbContext = dbContext;
        _templateCache = templateCache;
    }

    public async Task<TemplateResult> Handle(RollbackTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.Templates
            .Where(t => t.Id == request.TemplateId
                        && t.TenantId == request.TenantId
                        && t.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
            throw new NotFoundException($"Template with ID '{request.TemplateId}' not found.");

        var targetVersion = await _dbContext.TemplateVersions
            .AsNoTracking()
            .Where(v => v.TemplateId == request.TemplateId && v.Version == request.TargetVersion)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetVersion is null)
            throw new NotFoundException($"Template version {request.TargetVersion} not found for template '{request.TemplateId}'.");

        // Snapshot current state before rollback
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

        // Apply rollback content
        template.Name = targetVersion.Name;
        template.SubjectTemplate = targetVersion.Subject;
        template.HtmlBody = targetVersion.HtmlBody ?? string.Empty;
        template.TextBody = targetVersion.TextBody;
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
