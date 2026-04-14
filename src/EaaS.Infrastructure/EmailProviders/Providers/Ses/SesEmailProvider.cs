using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using EaaS.Domain.Providers;
using EaaS.Infrastructure.EmailProviders.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendEmailRequest = EaaS.Domain.Providers.SendEmailRequest;

namespace EaaS.Infrastructure.EmailProviders.Providers.Ses;

/// <summary>
/// Adapter wrapping AWS SES v2 behind <see cref="IEmailProvider"/>. Phase 0 adapter — no
/// behaviour change from the pre-abstraction <c>SesEmailService</c>. Domain-identity
/// operations were split into <see cref="SesDomainIdentityService"/> per ISP.
/// </summary>
public sealed partial class SesEmailProvider : IEmailProvider
{
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private readonly ILogger<SesEmailProvider> _logger;
    private readonly string? _configurationSetName;

    public SesEmailProvider(
        IAmazonSimpleEmailServiceV2 sesClient,
        IOptions<SesOptions> options,
        ILogger<SesEmailProvider> logger)
    {
        _sesClient = sesClient;
        _logger = logger;
        _configurationSetName = options.Value.ConfigurationSetName;
    }

    public string ProviderKey => EmailProviderConfigKeys.ProviderKeys.Ses;

    public EmailProviderCapability Capabilities =>
        EmailProviderCapability.Attachments
        | EmailProviderCapability.SendRaw
        | EmailProviderCapability.DomainIdentity
        | EmailProviderCapability.Tags
        | EmailProviderCapability.Templates;

    public async Task<EmailSendOutcome> SendAsync(
        SendEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.To is null || request.To.Count == 0)
            throw new EmailValidationException("At least one recipient is required.");
        if (string.IsNullOrWhiteSpace(request.From))
            throw new EmailValidationException("Sender address is required.");

        try
        {
            var destination = new Destination { ToAddresses = request.To.ToList() };
            if (request.Cc is { Count: > 0 })
                destination.CcAddresses = request.Cc.ToList();
            if (request.Bcc is { Count: > 0 })
                destination.BccAddresses = request.Bcc.ToList();

            var fromAddress = string.IsNullOrWhiteSpace(request.FromName)
                ? request.From
                : $"{request.FromName} <{request.From}>";

            var sesRequest = new Amazon.SimpleEmailV2.Model.SendEmailRequest
            {
                FromEmailAddress = fromAddress,
                Destination = destination,
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = request.Subject },
                        Body = new Body
                        {
                            Html = request.HtmlBody != null ? new Content { Data = request.HtmlBody } : null,
                            Text = request.TextBody != null ? new Content { Data = request.TextBody } : null
                        }
                    }
                },
                ConfigurationSetName = request.ConfigurationSetName ?? _configurationSetName
            };

            var response = await _sesClient.SendEmailAsync(sesRequest, cancellationToken);

            LogEmailSent(_logger, response.MessageId);

            return new EmailSendOutcome(true, response.MessageId, null, null, false);
        }
        catch (Exception ex)
        {
            LogEmailSendFailed(_logger, ex);
            return new EmailSendOutcome(false, null, ex.GetType().Name, ex.Message, IsRetryable(ex));
        }
    }

    public async Task<EmailSendOutcome> SendRawAsync(
        SendRawEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MimeMessage is null)
            throw new EmailValidationException("MIME message stream is required.");

        try
        {
            using var memoryStream = new MemoryStream();
            await request.MimeMessage.CopyToAsync(memoryStream, cancellationToken);

            var sesRequest = new Amazon.SimpleEmailV2.Model.SendEmailRequest
            {
                Content = new EmailContent
                {
                    Raw = new RawMessage { Data = memoryStream }
                },
                ConfigurationSetName = request.ConfigurationSetName ?? _configurationSetName
            };

            var response = await _sesClient.SendEmailAsync(sesRequest, cancellationToken);

            LogRawEmailSent(_logger, response.MessageId);

            return new EmailSendOutcome(true, response.MessageId, null, null, false);
        }
        catch (Exception ex)
        {
            LogRawEmailSendFailed(_logger, ex);
            return new EmailSendOutcome(false, null, ex.GetType().Name, ex.Message, IsRetryable(ex));
        }
    }

    private static bool IsRetryable(Exception ex) => ex is
        AmazonSimpleEmailServiceV2Exception { StatusCode: System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable }
        or TaskCanceledException
        or TimeoutException
        or HttpRequestException;

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent via SES, MessageId: {MessageId}")]
    private static partial void LogEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email via SES")]
    private static partial void LogEmailSendFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Raw email sent via SES, MessageId: {MessageId}")]
    private static partial void LogRawEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send raw email via SES")]
    private static partial void LogRawEmailSendFailed(ILogger logger, Exception ex);
}
