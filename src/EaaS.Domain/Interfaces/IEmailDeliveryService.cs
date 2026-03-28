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
    /// Sends an email via SES with optional CC/BCC recipients.
    /// </summary>
    Task<SendEmailResult> SendEmailAsync(
        string from,
        IReadOnlyList<string> recipients,
        IReadOnlyList<string>? ccRecipients,
        IReadOnlyList<string>? bccRecipients,
        string subject,
        string? htmlBody,
        string? textBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a raw MIME email via SES (used when attachments are present).
    /// </summary>
    Task<SendEmailResult> SendRawEmailAsync(Stream mimeMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an email identity (domain) from SES.
    /// </summary>
    Task DeleteDomainIdentityAsync(string domain, CancellationToken cancellationToken = default);
}

public record DomainIdentityResult(bool Success, string? IdentityArn, IReadOnlyList<DkimToken> DkimTokens, string? ErrorMessage);

public record DkimToken(string Token);

public record DomainVerificationResult(bool Success, bool IsVerified, IReadOnlyList<DkimTokenStatus> DkimStatuses, string? ErrorMessage);

public record DkimTokenStatus(string Token, bool IsVerified);

public record SendEmailResult(bool Success, string? MessageId, string? ErrorMessage);
