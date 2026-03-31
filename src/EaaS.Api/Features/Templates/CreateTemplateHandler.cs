using EaaS.Domain.Entities;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class CreateTemplateHandler : IRequestHandler<CreateTemplateCommand, TemplateResult>
{
    private readonly AppDbContext _dbContext;

    public CreateTemplateHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TemplateResult> Handle(CreateTemplateCommand request, CancellationToken cancellationToken)
    {
        // Check uniqueness per tenant
        var nameExists = await _dbContext.Templates
            .AsNoTracking()
            .AnyAsync(t => t.TenantId == request.TenantId
                           && t.Name == request.Name
                           && t.DeletedAt == null, cancellationToken);

        if (nameExists)
            throw new EaaS.Domain.Exceptions.ConflictException($"Template with name '{request.Name}' already exists.");

        var now = DateTime.UtcNow;
        var template = new Template
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Name = request.Name,
            SubjectTemplate = request.SubjectTemplate,
            HtmlBody = request.HtmlBody,
            TextBody = request.TextBody,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
