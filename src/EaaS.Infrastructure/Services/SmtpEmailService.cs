using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EaaS.Infrastructure.Services;

public sealed partial class SmtpEmailService : IDomainIdentityService, IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<DomainIdentityResult> CreateDomainIdentityAsync(string domain, CancellationToken cancellationToken = default)
    {
        LogDomainIdentityCreated(_logger, domain);

        // In local dev, domains are always "verified" — return mock DKIM tokens
        var dkimTokens = new List<DkimToken>
        {
            new($"local-dkim-token-1-{domain.Replace(".", "-")}"),
            new($"local-dkim-token-2-{domain.Replace(".", "-")}"),
            new($"local-dkim-token-3-{domain.Replace(".", "-")}")
        };

        return Task.FromResult(new DomainIdentityResult(
            Success: true,
            IdentityArn: null,
            DkimTokens: dkimTokens,
            ErrorMessage: null));
    }

    public Task<DomainVerificationResult> GetDomainVerificationStatusAsync(string domain, CancellationToken cancellationToken = default)
    {
        LogDomainVerificationChecked(_logger, domain);

        // In local dev, all domains are verified
        var dkimStatuses = new List<DkimTokenStatus>
        {
            new($"local-dkim-token-1-{domain.Replace(".", "-")}", true),
            new($"local-dkim-token-2-{domain.Replace(".", "-")}", true),
            new($"local-dkim-token-3-{domain.Replace(".", "-")}", true)
        };

        return Task.FromResult(new DomainVerificationResult(
            Success: true,
            IsVerified: true,
            DkimStatuses: dkimStatuses,
            ErrorMessage: null));
    }

    public async Task<SendEmailResult> SendEmailAsync(
        string from,
        IReadOnlyList<string> recipients,
        IReadOnlyList<string>? ccRecipients,
        IReadOnlyList<string>? bccRecipients,
        string subject,
        string? htmlBody,
        string? textBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from));

            foreach (var to in recipients)
                message.To.Add(MailboxAddress.Parse(to));

            if (ccRecipients is { Count: > 0 })
                foreach (var cc in ccRecipients)
                    message.Cc.Add(MailboxAddress.Parse(cc));

            if (bccRecipients is { Count: > 0 })
                foreach (var bcc in bccRecipients)
                    message.Bcc.Add(MailboxAddress.Parse(bcc));

            message.Subject = subject;

            var bodyBuilder = new BodyBuilder();
            if (htmlBody is not null)
                bodyBuilder.HtmlBody = htmlBody;
            if (textBody is not null)
                bodyBuilder.TextBody = textBody;

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl, cancellationToken);

            if (_settings.Username is not null && _settings.Password is not null)
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

            var response = await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            var messageId = message.MessageId ?? Guid.NewGuid().ToString();
            LogEmailSent(_logger, messageId);

            return new SendEmailResult(true, messageId, null);
        }
        catch (Exception ex)
        {
            LogEmailSendFailed(_logger, ex);
            return new SendEmailResult(false, null, ex.Message);
        }
    }

    public async Task<SendEmailResult> SendRawEmailAsync(Stream mimeMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await MimeMessage.LoadAsync(mimeMessage, cancellationToken);

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl, cancellationToken);

            if (_settings.Username is not null && _settings.Password is not null)
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            var messageId = message.MessageId ?? Guid.NewGuid().ToString();
            LogRawEmailSent(_logger, messageId);

            return new SendEmailResult(true, messageId, null);
        }
        catch (Exception ex)
        {
            LogRawEmailSendFailed(_logger, ex);
            return new SendEmailResult(false, null, ex.Message);
        }
    }

    public Task DeleteDomainIdentityAsync(string domain, CancellationToken cancellationToken = default)
    {
        LogDomainIdentityDeleted(_logger, domain);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMTP: domain identity created for {Domain} (local dev — auto-verified)")]
    private static partial void LogDomainIdentityCreated(ILogger logger, string domain);

    [LoggerMessage(Level = LogLevel.Information, Message = "SMTP: domain verification checked for {Domain} (local dev — always verified)")]
    private static partial void LogDomainVerificationChecked(ILogger logger, string domain);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent via SMTP (Mailpit), MessageId: {MessageId}")]
    private static partial void LogEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email via SMTP")]
    private static partial void LogEmailSendFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Raw email sent via SMTP (Mailpit), MessageId: {MessageId}")]
    private static partial void LogRawEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send raw email via SMTP")]
    private static partial void LogRawEmailSendFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "SMTP: domain identity deleted for {Domain} (local dev — no-op)")]
    private static partial void LogDomainIdentityDeleted(ILogger logger, string domain);
}
