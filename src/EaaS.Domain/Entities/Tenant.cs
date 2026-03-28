namespace EaaS.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<SendingDomain> Domains { get; set; } = new List<SendingDomain>();
    public ICollection<Template> Templates { get; set; } = new List<Template>();
    public ICollection<Email> Emails { get; set; } = new List<Email>();
    public ICollection<SuppressionEntry> SuppressionEntries { get; set; } = new List<SuppressionEntry>();
    public ICollection<Webhook> Webhooks { get; set; } = new List<Webhook>();
}
