namespace EaaS.Domain.Interfaces;

/// <summary>
/// Result of verifying a sending-domain identity and the status of each DKIM token.
/// </summary>
public record DomainVerificationResult(bool Success, bool IsVerified, IReadOnlyList<DkimTokenStatus> DkimStatuses, string? ErrorMessage);
