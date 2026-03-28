using EaaS.Domain.Entities;

namespace EaaS.Domain.Interfaces;

public interface IEmailDeliveryService
{
    /// <summary>
    /// Creates a domain identity in SES and returns DKIM tokens.
    /// </summary>
    Task<DomainIdentityResult> CreateDomainIdentityAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current verification status of a domain from SES.
    /// </summary>
    Task<DomainVerificationResult> GetDomainVerificationStatusAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an email via SES. (Stub for Phase 4, fully implemented in Phase 5.)
    /// </summary>
    Task<SendEmailResult> SendEmailAsync(string from, IReadOnlyList<string> recipients, string subject, string? htmlBody, string? textBody, CancellationToken cancellationToken = default);
}

public record DomainIdentityResult(bool Success, string? IdentityArn, IReadOnlyList<DkimToken> DkimTokens, string? ErrorMessage);

public record DkimToken(string Token);

public record DomainVerificationResult(bool Success, bool IsVerified, IReadOnlyList<DkimTokenStatus> DkimStatuses, string? ErrorMessage);

public record DkimTokenStatus(string Token, bool IsVerified);

public record SendEmailResult(bool Success, string? MessageId, string? ErrorMessage);
