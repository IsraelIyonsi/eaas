namespace EaaS.Domain.Interfaces;

public interface ITemplateRenderingService
{
    Task<RenderedTemplate> RenderAsync(
        string subjectTemplate,
        string htmlBody,
        string? textBody,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default);
}

public record RenderedTemplate(string Subject, string HtmlBody, string? TextBody);
