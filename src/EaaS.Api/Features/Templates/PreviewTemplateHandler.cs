using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class PreviewTemplateHandler : IRequestHandler<PreviewTemplateCommand, PreviewTemplateResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ITemplateRenderingService _templateRenderingService;

    public PreviewTemplateHandler(AppDbContext dbContext, ITemplateRenderingService templateRenderingService)
    {
        _dbContext = dbContext;
        _templateRenderingService = templateRenderingService;
    }

    public async Task<PreviewTemplateResult> Handle(PreviewTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.Templates
            .AsNoTracking()
            .Where(t => t.Id == request.TemplateId && t.TenantId == request.TenantId && t.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EaaS.Domain.Exceptions.NotFoundException($"Template with id '{request.TemplateId}' not found.");

        var variables = request.Variables ?? new Dictionary<string, object>();

        var rendered = await _templateRenderingService.RenderAsync(
            template.SubjectTemplate,
            template.HtmlBody,
            template.TextBody,
            variables,
            cancellationToken);

        return new PreviewTemplateResult(rendered.Subject, rendered.HtmlBody, rendered.TextBody);
    }
}
