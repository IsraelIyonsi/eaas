namespace EaaS.Infrastructure.Messaging.Contracts;

public sealed record SendEmailMessage
{
    public Guid EmailId { get; init; }
    public Guid TenantId { get; init; }
    public string From { get; init; } = string.Empty;
    public string? FromName { get; init; }
    public string To { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string? HtmlBody { get; init; }
    public string? TextBody { get; init; }
    public Guid? TemplateId { get; init; }
    public string? Variables { get; init; }
    public string[] Tags { get; init; } = Array.Empty<string>();
    public string? Metadata { get; init; }
}
