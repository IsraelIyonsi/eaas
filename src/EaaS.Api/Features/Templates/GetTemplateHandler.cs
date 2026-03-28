using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class GetTemplateHandler : IRequestHandler<GetTemplateQuery, TemplateResult>
{
    private readonly AppDbContext _dbContext;

    public GetTemplateHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TemplateResult> Handle(GetTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.Templates
            .AsNoTracking()
            .Where(t => t.TenantId == request.TenantId
                        && t.Id == request.TemplateId
                        && t.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
            throw new KeyNotFoundException($"Template with ID '{request.TemplateId}' not found.");

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
