namespace EaaS.Domain.Interfaces;

public record DomainIdentityResult(bool Success, string? IdentityArn, IReadOnlyList<DkimToken> DkimTokens, string? ErrorMessage);

public record DkimToken(string Token);

public record DomainVerificationResult(bool Success, bool IsVerified, IReadOnlyList<DkimTokenStatus> DkimStatuses, string? ErrorMessage);

public record DkimTokenStatus(string Token, bool IsVerified);

public record SendEmailResult(bool Success, string? MessageId, string? ErrorMessage);
