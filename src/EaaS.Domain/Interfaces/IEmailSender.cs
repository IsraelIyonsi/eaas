namespace EaaS.Domain.Interfaces;

public interface IEmailSender
{
    Task<SendEmailResult> SendEmailAsync(
        string from,
        IReadOnlyList<string> recipients,
        IReadOnlyList<string>? ccRecipients,
        IReadOnlyList<string>? bccRecipients,
        string subject,
        string? htmlBody,
        string? textBody,
        CancellationToken cancellationToken = default);

    Task<SendEmailResult> SendRawEmailAsync(Stream mimeMessage, CancellationToken cancellationToken = default);
}
