using EaaS.Domain.Interfaces;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.Services;

public sealed partial class TemplateRenderingService : ITemplateRenderingService
{
    private readonly ILogger<TemplateRenderingService> _logger;
    private static readonly FluidParser Parser = new();

    public TemplateRenderingService(ILogger<TemplateRenderingService> logger)
    {
        _logger = logger;
    }

    public async Task<RenderedTemplate> RenderAsync(
        string subjectTemplate,
        string htmlBody,
        string? textBody,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        var context = new TemplateContext();

        foreach (var (key, value) in variables)
        {
            context.SetValue(key, FluidValue.Create(value, context.Options));
        }

        var renderedSubject = await RenderTemplateStringAsync(subjectTemplate, context);
        var renderedHtml = await RenderTemplateStringAsync(htmlBody, context);
        var renderedText = textBody is not null
            ? await RenderTemplateStringAsync(textBody, context)
            : null;

        LogTemplateRendered(_logger);

        return new RenderedTemplate(renderedSubject, renderedHtml, renderedText);
    }

    private static async Task<string> RenderTemplateStringAsync(string template, TemplateContext context)
    {
        if (!Parser.TryParse(template, out var fluidTemplate, out var error))
        {
            throw new InvalidOperationException($"Template syntax error: {error}");
        }

        return await fluidTemplate.RenderAsync(context);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Template rendered successfully")]
    private static partial void LogTemplateRendered(ILogger logger);
}
