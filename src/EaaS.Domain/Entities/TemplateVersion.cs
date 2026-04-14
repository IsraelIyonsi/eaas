namespace EaaS.Domain.Entities;

public sealed class TemplateVersion
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    // NOTE (MED-6): see Template.cs — CLR names intentionally kept as HtmlBody/TextBody.
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Template Template { get; set; } = null!;
}
