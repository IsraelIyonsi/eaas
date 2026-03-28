using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

/// <summary>
/// Represents a sending domain entity. Named SendingDomain to avoid conflict with System.Domain.
/// Maps to the "domains" table in the database.
/// </summary>
public class SendingDomain
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string DomainName { get; set; } = string.Empty;
    public DomainStatus Status { get; set; } = DomainStatus.PendingVerification;
    public string? SesIdentityArn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<DnsRecord> DnsRecords { get; set; } = new List<DnsRecord>();
}
