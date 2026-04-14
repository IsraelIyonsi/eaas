using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Metrics;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace EaaS.Infrastructure.Messaging;

public sealed partial class SendEmailConsumer : IConsumer<SendEmailMessage>
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailSender _emailDeliveryService;
    private readonly ITemplateRenderingService _templateRenderingService;
    private readonly TrackingPixelInjector? _pixelInjector;
    private readonly ClickTrackingLinkRewriter? _linkRewriter;
    private readonly ListUnsubscribeService? _unsubscribeService;
    private readonly EmailFooterInjector? _footerInjector;
    private readonly ILogger<SendEmailConsumer> _logger;

    public SendEmailConsumer(
        AppDbContext dbContext,
        IEmailSender emailDeliveryService,
        ITemplateRenderingService templateRenderingService,
        ILogger<SendEmailConsumer> logger,
        TrackingPixelInjector? pixelInjector = null,
        ClickTrackingLinkRewriter? linkRewriter = null,
        ListUnsubscribeService? unsubscribeService = null,
        EmailFooterInjector? footerInjector = null)
    {
        _dbContext = dbContext;
        _emailDeliveryService = emailDeliveryService;
        _templateRenderingService = templateRenderingService;
        _logger = logger;
        _pixelInjector = pixelInjector;
        _linkRewriter = linkRewriter;
        _unsubscribeService = unsubscribeService;
        _footerInjector = footerInjector;
    }

    public async Task Consume(ConsumeContext<SendEmailMessage> context)
    {
        var message = context.Message;
        LogReceivedMessage(_logger, message.EmailId);

        // 1. Load Email entity from DB
        var email = await _dbContext.Emails
            .FirstOrDefaultAsync(e => e.Id == message.EmailId, context.CancellationToken);

        if (email is null)
        {
            LogEmailNotFound(_logger, message.EmailId);
            return;
        }

        // Idempotency guard — skip if in a terminal state (already delivered, bounced, or complained)
        if (email.Status is EmailStatus.Delivered or EmailStatus.Bounced or EmailStatus.Complained)
        {
            LogSkippingDuplicate(_logger, message.EmailId, email.Status);
            return;
        }

        var subject = email.Subject;
        var htmlBody = email.HtmlBody;
        var textBody = email.TextBody;

        // Track temp files for cleanup
        var tempFiles = new List<string>();

        try
        {
            // 2. If templateId provided: load template and render
            if (message.TemplateId.HasValue)
            {
                var template = await _dbContext.Templates
                    .AsNoTracking()
                    .Where(t => t.Id == message.TemplateId.Value && t.DeletedAt == null)
                    .FirstOrDefaultAsync(context.CancellationToken);

                if (template is null)
                {
                    await UpdateEmailStatus(email, EmailStatus.Failed, $"Template '{message.TemplateId}' not found.", context.CancellationToken);
                    return;
                }

                var variables = !string.IsNullOrWhiteSpace(message.Variables)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(message.Variables) ?? new Dictionary<string, object>()
                    : new Dictionary<string, object>();

                var rendered = await _templateRenderingService.RenderAsync(
                    template.SubjectTemplate,
                    template.HtmlBody,
                    template.TextBody,
                    variables,
                    context.CancellationToken);

                subject = rendered.Subject;
                htmlBody = rendered.HtmlBody;
                textBody = rendered.TextBody;
            }

            // 3. Open tracking: inject tracking pixel
            if (message.TrackOpens && !string.IsNullOrWhiteSpace(htmlBody) && _pixelInjector is not null)
            {
                htmlBody = _pixelInjector.InjectTrackingPixel(htmlBody, message.EmailId);
                LogTrackingPixelInjected(_logger, message.EmailId);
            }

            // 4. Click tracking: rewrite links
            if (message.TrackClicks && !string.IsNullOrWhiteSpace(htmlBody) && _linkRewriter is not null)
            {
                htmlBody = await _linkRewriter.RewriteLinksAsync(htmlBody, message.EmailId, context.CancellationToken);
                LogClickTrackingApplied(_logger, message.EmailId);
            }

            // 4b. CAN-SPAM §7704(a)(5) footer + RFC 8058 List-Unsubscribe (injected for ALL
            // emails until we introduce an explicit Category field — safe default).
            string? mailtoUnsub = null;
            string? httpsUnsub = null;
            if (_unsubscribeService is not null && _footerInjector is not null)
            {
                var sentAt = DateTime.UtcNow;
                var primaryRecipient = ExtractPrimaryRecipient(message.To);
                if (!string.IsNullOrWhiteSpace(primaryRecipient))
                {
                    var unsubscribeToken = _unsubscribeService.GenerateToken(message.TenantId, primaryRecipient, sentAt);
                    mailtoUnsub = _unsubscribeService.MailtoUnsubscribe(unsubscribeToken);
                    httpsUnsub = _unsubscribeService.HttpsUnsubscribe(unsubscribeToken);

                    var tenant = await _dbContext.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == message.TenantId, context.CancellationToken);

                    var displayName = tenant?.LegalEntityName
                        ?? tenant?.CompanyName
                        ?? tenant?.Name
                        ?? "Sender";
                    var postalAddress = tenant?.PostalAddress ?? "[Postal address pending]";

                    htmlBody = _footerInjector.InjectHtmlFooter(htmlBody, displayName, postalAddress, httpsUnsub);
                    textBody = _footerInjector.InjectTextFooter(textBody, displayName, postalAddress, httpsUnsub);
                }
            }

            // 5. Update status to Sending
            await UpdateEmailStatus(email, EmailStatus.Sending, null, context.CancellationToken);

            // 6. Parse recipients (To, CC, BCC)
            var recipients = JsonSerializer.Deserialize<List<string>>(message.To) ?? new List<string>();
            var ccRecipients = !string.IsNullOrWhiteSpace(message.CcEmails) && message.CcEmails != "[]"
                ? JsonSerializer.Deserialize<List<string>>(message.CcEmails)
                : null;
            var bccRecipients = !string.IsNullOrWhiteSpace(message.BccEmails) && message.BccEmails != "[]"
                ? JsonSerializer.Deserialize<List<string>>(message.BccEmails)
                : null;

            // 7. Parse attachments
            var attachments = !string.IsNullOrWhiteSpace(message.Attachments) && message.Attachments != "[]"
                ? JsonSerializer.Deserialize<List<AttachmentMetadata>>(message.Attachments) ?? new List<AttachmentMetadata>()
                : new List<AttachmentMetadata>();

            // Collect temp file paths for cleanup
            foreach (var att in attachments)
            {
                if (!string.IsNullOrWhiteSpace(att.TempPath))
                    tempFiles.Add(att.TempPath);
            }

            SendEmailResult result;

            // Whenever we need custom headers (List-Unsubscribe) OR have attachments,
            // we must use the raw-MIME path — SES SendEmail does not support custom headers.
            var needsRaw = attachments.Count > 0 || mailtoUnsub is not null;

            if (needsRaw)
            {
                using var mimeStream = BuildMimeMessage(
                    message.From,
                    message.FromName,
                    recipients,
                    ccRecipients,
                    bccRecipients,
                    subject,
                    htmlBody,
                    textBody,
                    attachments,
                    mailtoUnsub,
                    httpsUnsub);

                result = await _emailDeliveryService.SendRawEmailAsync(mimeStream, context.CancellationToken);
            }
            else
            {
                // Standard simple send (no headers needed)
                result = await _emailDeliveryService.SendEmailAsync(
                    message.From,
                    recipients,
                    ccRecipients,
                    bccRecipients,
                    subject,
                    htmlBody,
                    textBody,
                    context.CancellationToken);
            }

            if (result.Success)
            {
                email.SesMessageId = result.MessageId;
                email.SentAt = DateTime.UtcNow;
                await UpdateEmailStatus(email, EmailStatus.Sent, null, context.CancellationToken);

                EmailMetrics.EmailsSent.WithLabels(message.TenantId.ToString(), "success").Inc();
                LogEmailSent(_logger, message.EmailId, result.MessageId!);
            }
            else
            {
                await UpdateEmailStatus(email, EmailStatus.Failed, result.ErrorMessage, context.CancellationToken);
                EmailMetrics.EmailsSent.WithLabels(message.TenantId.ToString(), "failed").Inc();
                LogEmailFailed(_logger, message.EmailId, result.ErrorMessage ?? "Unknown error");

                throw new InvalidOperationException($"SES delivery failed: {result.ErrorMessage}");
            }
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw for MassTransit retry
        }
        catch (Exception ex)
        {
            await UpdateEmailStatus(email, EmailStatus.Failed, ex.Message, context.CancellationToken);
            LogEmailException(_logger, ex, message.EmailId);
            throw; // Re-throw for MassTransit retry
        }
        finally
        {
            // Clean up temp attachment files
            CleanupTempFiles(tempFiles);
        }
    }

    private static MemoryStream BuildMimeMessage(
        string from,
        string? fromName,
        List<string> toRecipients,
        List<string>? ccRecipients,
        List<string>? bccRecipients,
        string subject,
        string? htmlBody,
        string? textBody,
        List<AttachmentMetadata> attachments,
        string? listUnsubscribeMailto = null,
        string? listUnsubscribeHttps = null)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(fromName ?? string.Empty, from));

        // RFC 2369 List-Unsubscribe + RFC 8058 One-Click
        if (listUnsubscribeMailto is not null || listUnsubscribeHttps is not null)
        {
            var parts = new List<string>(2);
            if (listUnsubscribeMailto is not null) parts.Add($"<{listUnsubscribeMailto}>");
            if (listUnsubscribeHttps is not null) parts.Add($"<{listUnsubscribeHttps}>");
            mimeMessage.Headers.Add("List-Unsubscribe", string.Join(", ", parts));
            mimeMessage.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        foreach (var to in toRecipients)
            mimeMessage.To.Add(MailboxAddress.Parse(to));

        if (ccRecipients is not null)
        {
            foreach (var cc in ccRecipients)
                mimeMessage.Cc.Add(MailboxAddress.Parse(cc));
        }

        if (bccRecipients is not null)
        {
            foreach (var bcc in bccRecipients)
                mimeMessage.Bcc.Add(MailboxAddress.Parse(bcc));
        }

        mimeMessage.Subject = subject;

        var multipart = new Multipart("mixed");

        // Add body
        if (!string.IsNullOrWhiteSpace(htmlBody))
        {
            var htmlPart = new TextPart("html") { Text = htmlBody };
            if (!string.IsNullOrWhiteSpace(textBody))
            {
                var alternative = new Multipart("alternative");
                alternative.Add(new TextPart("plain") { Text = textBody });
                alternative.Add(htmlPart);
                multipart.Add(alternative);
            }
            else
            {
                multipart.Add(htmlPart);
            }
        }
        else if (!string.IsNullOrWhiteSpace(textBody))
        {
            multipart.Add(new TextPart("plain") { Text = textBody });
        }

        // Add attachments — read into MemoryStreams to avoid leaking FileStream handles
        foreach (var attachment in attachments)
        {
            if (!File.Exists(attachment.TempPath))
                continue;

            var fileBytes = File.ReadAllBytes(attachment.TempPath);
            var memStream = new MemoryStream(fileBytes);

            var contentType = ContentType.Parse(attachment.ContentType);
            var mimePart = new MimePart(contentType)
            {
                Content = new MimeContent(memStream),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = attachment.Filename
            };

            multipart.Add(mimePart);
        }

        mimeMessage.Body = multipart;

        var stream = new MemoryStream();
        mimeMessage.WriteTo(stream);
        stream.Position = 0;
        return stream;
    }

    private void CleanupTempFiles(List<string> tempFiles)
    {
        foreach (var filePath in tempFiles)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception ex)
            {
                LogTempFileCleanupFailed(_logger, ex, filePath);
            }
        }

        // Try to clean up the parent directory if empty
        if (tempFiles.Count > 0)
        {
            try
            {
                var dir = Path.GetDirectoryName(tempFiles[0]);
                if (dir is not null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private async Task UpdateEmailStatus(Email email, EmailStatus status, string? errorMessage, CancellationToken cancellationToken)
    {
        email.Status = status;
        if (errorMessage is not null)
            email.ErrorMessage = errorMessage;

        var eventType = status switch
        {
            EmailStatus.Sending => EventType.Queued,
            EmailStatus.Sent => EventType.Sent,
            EmailStatus.Delivered => EventType.Delivered,
            EmailStatus.Failed => EventType.Failed,
            _ => EventType.Queued
        };

        _dbContext.EmailEvents.Add(new EmailEvent
        {
            Id = Guid.NewGuid(),
            EmailId = email.Id,
            EventType = eventType,
            Data = errorMessage is not null
                ? JsonSerializer.Serialize(new { error = errorMessage })
                : "{}",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Received SendEmailMessage for EmailId={EmailId}")]
    private static partial void LogReceivedMessage(ILogger logger, Guid emailId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email entity not found for EmailId={EmailId}")]
    private static partial void LogEmailNotFound(ILogger logger, Guid emailId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping duplicate delivery for EmailId={EmailId}, current status={Status}")]
    private static partial void LogSkippingDuplicate(ILogger logger, Guid emailId, EmailStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tracking pixel injected for EmailId={EmailId}")]
    private static partial void LogTrackingPixelInjected(ILogger logger, Guid emailId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Click tracking applied for EmailId={EmailId}")]
    private static partial void LogClickTrackingApplied(ILogger logger, Guid emailId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent to SES for EmailId={EmailId}, SesMessageId={SesMessageId}")]
    private static partial void LogEmailSent(ILogger logger, Guid emailId, string sesMessageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Email delivery failed for EmailId={EmailId}: {Error}")]
    private static partial void LogEmailFailed(ILogger logger, Guid emailId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error processing EmailId={EmailId}")]
    private static partial void LogEmailException(ILogger logger, Exception ex, Guid emailId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to clean up temp file {FilePath}")]
    private static partial void LogTempFileCleanupFailed(ILogger logger, Exception ex, string filePath);

    private static string? ExtractPrimaryRecipient(string toJson)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(toJson);
            return list is { Count: > 0 } ? list[0] : null;
        }
        catch
        {
            return null;
        }
    }
}
