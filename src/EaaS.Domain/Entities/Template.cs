namespace EaaS.Domain.Entities;

public class Template
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    // NOTE (MED-6): Public API exposes these as `htmlTemplate` / `textTemplate`.
    // The CLR property + DB column names retain the legacy `HtmlBody` / `TextBody`
    // to avoid a destructive internal rename / schema migration. Public API DTOs
    // (CreateTemplateRequest/TemplateResult/etc.) use the new names; handlers bridge.
    public string HtmlBody { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? VariablesSchema { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<TemplateVersion> Versions { get; set; } = new List<TemplateVersion>();
}
