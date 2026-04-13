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
        var email = await _dbContext.Emails
            .AsNoTracking()
            .Include(e => e.Events)
            .Where(e => e.Id == request.Id && e.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (email is null)
            throw new NotFoundException($"Email with id '{request.Id}' not found.");

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
