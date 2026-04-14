namespace EaaS.Domain.Interfaces;

/// <summary>
/// Result of creating or looking up a sending-domain identity with an email provider.
/// </summary>
public record DomainIdentityResult(bool Success, string? IdentityArn, IReadOnlyList<DkimToken> DkimTokens, string? ErrorMessage);
