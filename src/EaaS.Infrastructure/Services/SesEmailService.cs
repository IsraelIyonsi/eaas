using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Services;

public sealed partial class SesEmailService : IDomainIdentityService, IEmailSender
{
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private readonly ILogger<SesEmailService> _logger;

    public SesEmailService(IAmazonSimpleEmailServiceV2 sesClient, ILogger<SesEmailService> logger)
    {
        _sesClient = sesClient;
        _logger = logger;
    }

    public async Task<DomainIdentityResult> CreateDomainIdentityAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CreateEmailIdentityRequest
            {
                EmailIdentity = domain,
                DkimSigningAttributes = new DkimSigningAttributes
                {
                    NextSigningKeyLength = DkimSigningKeyLength.RSA_2048_BIT
                }
            };

            var response = await _sesClient.CreateEmailIdentityAsync(request, cancellationToken);

            var dkimTokens = response.DkimAttributes?.Tokens?
                .Select(t => new DkimToken(t))
                .ToList() ?? new List<DkimToken>();

            LogDomainIdentityCreated(_logger, domain, dkimTokens.Count);

            return new DomainIdentityResult(
                Success: true,
                IdentityArn: null, // SES v2 CreateEmailIdentity doesn't return ARN directly
                DkimTokens: dkimTokens,
                ErrorMessage: null);
        }
        catch (AlreadyExistsException)
        {
            LogDomainAlreadyExists(_logger, domain);
            return new DomainIdentityResult(false, null, Array.Empty<DkimToken>(), $"Domain '{domain}' already exists in SES.");
        }
        catch (Exception ex)
        {
            LogDomainIdentityFailed(_logger, ex, domain);
            return new DomainIdentityResult(false, null, Array.Empty<DkimToken>(), ex.Message);
        }
    }

    public async Task<DomainVerificationResult> GetDomainVerificationStatusAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetEmailIdentityRequest { EmailIdentity = domain };
            var response = await _sesClient.GetEmailIdentityAsync(request, cancellationToken);

            var isVerified = response.VerifiedForSendingStatus;

            var dkimStatuses = response.DkimAttributes?.Tokens?
                .Select(t => new DkimTokenStatus(t, response.DkimAttributes.Status == DkimStatus.SUCCESS))
                .ToList() ?? new List<DkimTokenStatus>();

            LogDomainVerificationChecked(_logger, domain, isVerified);

            return new DomainVerificationResult(
                Success: true,
                IsVerified: isVerified,
                DkimStatuses: dkimStatuses,
                ErrorMessage: null);
        }
        catch (NotFoundException)
        {
            return new DomainVerificationResult(false, false, Array.Empty<DkimTokenStatus>(), $"Domain '{domain}' not found in SES.");
        }
        catch (Exception ex)
        {
            LogDomainVerificationFailed(_logger, ex, domain);
            return new DomainVerificationResult(false, false, Array.Empty<DkimTokenStatus>(), ex.Message);
        }
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
            var destination = new Destination { ToAddresses = recipients.ToList() };

            if (ccRecipients is { Count: > 0 })
                destination.CcAddresses = ccRecipients.ToList();

            if (bccRecipients is { Count: > 0 })
                destination.BccAddresses = bccRecipients.ToList();

            var request = new SendEmailRequest
            {
                FromEmailAddress = from,
                Destination = destination,
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = subject },
                        Body = new Body
                        {
                            Html = htmlBody != null ? new Content { Data = htmlBody } : null,
                            Text = textBody != null ? new Content { Data = textBody } : null
                        }
                    }
                }
            };

            var response = await _sesClient.SendEmailAsync(request, cancellationToken);

            LogEmailSent(_logger, response.MessageId);

            return new SendEmailResult(true, response.MessageId, null);
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
            using var memoryStream = new MemoryStream();
            await mimeMessage.CopyToAsync(memoryStream, cancellationToken);

            var request = new SendEmailRequest
            {
                Content = new EmailContent
                {
                    Raw = new RawMessage
                    {
                        Data = memoryStream
                    }
                }
            };

            var response = await _sesClient.SendEmailAsync(request, cancellationToken);

            LogRawEmailSent(_logger, response.MessageId);

            return new SendEmailResult(true, response.MessageId, null);
        }
        catch (Exception ex)
        {
            LogRawEmailSendFailed(_logger, ex);
            return new SendEmailResult(false, null, ex.Message);
        }
    }

    public async Task DeleteDomainIdentityAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sesClient.DeleteEmailIdentityAsync(
                new DeleteEmailIdentityRequest { EmailIdentity = domain },
                cancellationToken);
            LogDomainIdentityDeleted(_logger, domain);
        }
        catch (Exception ex)
        {
            LogDomainIdentityDeleteFailed(_logger, ex, domain);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SES domain identity created for {Domain} with {TokenCount} DKIM tokens")]
    private static partial void LogDomainIdentityCreated(ILogger logger, string domain, int tokenCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Domain {Domain} already exists in SES")]
    private static partial void LogDomainAlreadyExists(ILogger logger, string domain);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create SES domain identity for {Domain}")]
    private static partial void LogDomainIdentityFailed(ILogger logger, Exception ex, string domain);

    [LoggerMessage(Level = LogLevel.Information, Message = "Domain verification checked for {Domain}, verified: {IsVerified}")]
    private static partial void LogDomainVerificationChecked(ILogger logger, string domain, bool isVerified);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to check domain verification for {Domain}")]
    private static partial void LogDomainVerificationFailed(ILogger logger, Exception ex, string domain);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent via SES, MessageId: {MessageId}")]
    private static partial void LogEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email via SES")]
    private static partial void LogEmailSendFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Raw email sent via SES, MessageId: {MessageId}")]
    private static partial void LogRawEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send raw email via SES")]
    private static partial void LogRawEmailSendFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "SES domain identity deleted for {Domain}")]
    private static partial void LogDomainIdentityDeleted(ILogger logger, string domain);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete SES domain identity for {Domain}")]
    private static partial void LogDomainIdentityDeleteFailed(ILogger logger, Exception ex, string domain);
}
