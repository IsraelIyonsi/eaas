using EaaS.Domain.Interfaces;
using MimeKit;

namespace EaaS.Infrastructure.Services;

public sealed class MimeKitInboundParser : IInboundEmailParser
{
    public InboundParsedEmail Parse(Stream mimeStream)
    {
        var message = MimeMessage.Load(mimeStream);

        var from = message.From.Mailboxes.FirstOrDefault();
        var toAddresses = message.To.Mailboxes
            .Select(m => new EmailAddress(m.Address, m.Name))
            .ToList();
        var ccAddresses = message.Cc.Mailboxes
            .Select(m => new EmailAddress(m.Address, m.Name))
            .ToList();
        var bccAddresses = message.Bcc.Mailboxes
            .Select(m => new EmailAddress(m.Address, m.Name))
            .ToList();

        var headers = new Dictionary<string, string>();
        foreach (var header in message.Headers)
        {
            headers[header.Field] = header.Value;
        }

        var attachments = new List<ParsedAttachment>();
        foreach (var part in message.BodyParts.OfType<MimePart>())
        {
            var isAttachment = part.ContentDisposition?.Disposition == ContentDisposition.Attachment;
            var isInline = part.ContentId != null && !isAttachment;

            if (!isAttachment && !isInline)
                continue;

            if (part.Content is null)
                continue;

            var memoryStream = new MemoryStream();
            part.Content.DecodeTo(memoryStream);
            memoryStream.Position = 0;

            attachments.Add(new ParsedAttachment
            {
                Filename = part.FileName ?? $"attachment-{attachments.Count + 1}",
                ContentType = part.ContentType.MimeType,
                SizeBytes = memoryStream.Length,
                Content = memoryStream,
                ContentId = part.ContentId,
                IsInline = isInline
            });
        }

        return new InboundParsedEmail
        {
            FromEmail = from?.Address ?? string.Empty,
            FromName = from?.Name,
            ToAddresses = toAddresses,
            CcAddresses = ccAddresses,
            BccAddresses = bccAddresses,
            ReplyTo = message.ReplyTo.Mailboxes.FirstOrDefault()?.Address,
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
            Headers = headers,
            InReplyTo = message.InReplyTo,
            References = message.References != null ? string.Join(" ", message.References) : null,
            Attachments = attachments
        };
    }
}
