namespace EaaS.Domain.Interfaces;

/// <summary>
/// DKIM token value that must be published as a DNS record to authenticate outbound mail.
/// </summary>
public record DkimToken(string Token);
