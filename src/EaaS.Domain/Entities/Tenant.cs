using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public string? ContactEmail { get; set; }
    public string? CompanyName { get; set; }
    /// <summary>Registered legal entity name (CAN-SPAM §7704(a)(5), GDPR Art 13(1)(a)).</summary>
    public string? LegalEntityName { get; set; }
    /// <summary>Physical postal address required in every marketing email footer (CAN-SPAM §7704(a)(5)).</summary>
    public string? PostalAddress { get; set; }
    public int? MaxApiKeys { get; set; }
    public int? MaxDomainsCount { get; set; }
    public long? MonthlyEmailLimit { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PasswordHash { get; set; }
    public string? Notes { get; set; }
    /// <summary>Preferred email-provider key for this tenant's outbound sends (e.g. <c>"ses"</c>, <c>"mailgun"</c>). Nullable — when <c>null</c>, the global default from <c>EmailProviders:Routing:DefaultProvider</c> is used.</summary>
    public string? PreferredEmailProviderKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<SendingDomain> Domains { get; set; } = new List<SendingDomain>();
    public ICollection<Template> Templates { get; set; } = new List<Template>();
    public ICollection<Email> Emails { get; set; } = new List<Email>();
    public ICollection<SuppressionEntry> SuppressionEntries { get; set; } = new List<SuppressionEntry>();
    public ICollection<Webhook> Webhooks { get; set; } = new List<Webhook>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
