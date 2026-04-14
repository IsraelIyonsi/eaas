using EaaS.Domain.Exceptions;
using System.Text.Json;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Emails;

public sealed class GetEmailHandler : IRequestHandler<GetEmailQuery, EmailDetailResult>
{
    private readonly AppDbContext _dbContext;

    public GetEmailHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EmailDetailResult> Handle(GetEmailQuery request, CancellationToken cancellationToken)
    {
        // BUG-M3: accept either the internal GUID or the public `snx_` MessageId.
        // `snx_` is the documented prefix for send-side message identifiers; anything
        // else must be a GUID or the caller gets a clean 404 (no internal id enumeration).
        var query = _dbContext.Emails
            .AsNoTracking()
            .Include(e => e.Events)
            .Where(e => e.TenantId == request.TenantId);

        var identifier = request.Identifier ?? string.Empty;
        if (identifier.StartsWith("snx_", StringComparison.Ordinal))
        {
            query = query.Where(e => e.MessageId == identifier);
        }
        else if (Guid.TryParse(identifier, out var guid))
        {
            query = query.Where(e => e.Id == guid);
        }
        else
        {
            // Intentionally the same 404 surface as a missing record — do not echo
            // which lookup path the id failed against.
            throw new NotFoundException($"Email with id '{identifier}' not found.");
        }

        var email = await query.FirstOrDefaultAsync(cancellationToken);

        if (email is null)
            throw new NotFoundException($"Email with id '{identifier}' not found.");

        var toList = JsonSerializer.Deserialize<List<string>>(email.ToEmails) ?? new List<string>();
        var ccList = !string.IsNullOrWhiteSpace(email.CcEmails) && email.CcEmails != "[]"
            ? JsonSerializer.Deserialize<List<string>>(email.CcEmails)
            : null;
        var bccList = !string.IsNullOrWhiteSpace(email.BccEmails) && email.BccEmails != "[]"
            ? JsonSerializer.Deserialize<List<string>>(email.BccEmails)
            : null;

        string? templateName = null;
        if (email.TemplateId.HasValue)
        {
            templateName = await _dbContext.Templates
                .Where(t => t.Id == email.TemplateId.Value)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new EmailDetailResult(
            email.Id,
            email.MessageId,
            email.FromEmail,
            toList,
            ccList,
            bccList,
            email.Subject,
            email.Status.ToString().ToLowerInvariant(),
            email.HtmlBody,
            email.TextBody,
            email.TemplateId,
            templateName,
            email.Tags,
            email.Events
                .OrderBy(ev => ev.CreatedAt)
                .Select(ev => new EmailEventDto(
                    ev.Id,
                    ev.EventType.ToString().ToLowerInvariant(),
                    ev.Data,
                    ev.CreatedAt))
                .ToList(),
            email.CreatedAt,
            email.SentAt,
            email.DeliveredAt,
            email.OpenedAt,
            email.ClickedAt);
    }
}
