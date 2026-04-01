using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Messaging.Contracts;
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
    private readonly ILogger<SendEmailConsumer> _logger;

    public SendEmailConsumer(
        AppDbContext dbContext,
        IEmailSender emailDeliveryService,
        ITemplateRenderingService templateRenderingService,
        ILogger<SendEmailConsumer> logger,
        TrackingPixelInjector? pixelInjector = null,
        ClickTrackingLinkRewriter? linkRewriter = null)
    {
        _dbContext = dbContext;
        _emailDeliveryService = emailDeliveryService;
        _templateRenderingService = templateRenderingService;
        _logger = logger;
        _pixelInjector = pixelInjector;
        _linkRewriter = linkRewriter;
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

            if (attachments.Count > 0)
            {
                // 8a. Build MIME message with MimeKit and send raw
                using var mimeStream = BuildMimeMessage(
                    message.From,
                    message.FromName,
                    recipients,
                    ccRecipients,
                    bccRecipients,
                    subject,
                    htmlBody,
                    textBody,
                    attachments);

                result = await _emailDeliveryService.SendRawEmailAsync(mimeStream, context.CancellationToken);
            }
            else
            {
                // 8b. Standard simple send
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
                await UpdateEmailStatus(email, EmailStatus.Sending, null, context.CancellationToken);

                LogEmailSent(_logger, message.EmailId, result.MessageId!);
            }
            else
            {
                await UpdateEmailStatus(email, EmailStatus.Failed, result.ErrorMessage, context.CancellationToken);
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
        List<AttachmentMetadata> attachments)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(fromName ?? string.Empty, from));

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

        // Add attachments
        foreach (var attachment in attachments)
        {
            if (!File.Exists(attachment.TempPath))
                continue;

            var contentType = ContentType.Parse(attachment.ContentType);
            var mimePart = new MimePart(contentType)
            {
                Content = new MimeContent(File.OpenRead(attachment.TempPath)),
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
            EmailStatus.Sending => EventType.Sent,
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
}
