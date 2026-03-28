using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class DnsRecord
{
    public Guid Id { get; set; }
    public Guid DomainId { get; set; }
    public string RecordType { get; set; } = string.Empty;
    public string RecordName { get; set; } = string.Empty;
    public string RecordValue { get; set; } = string.Empty;
    public DnsRecordPurpose Purpose { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? ActualValue { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public SendingDomain Domain { get; set; } = null!;
}
