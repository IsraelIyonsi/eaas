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

        return new EmailDetailResult(
            email.Id,
            email.MessageId,
            email.FromEmail,
            toList,
            email.Subject,
            email.Status.ToString().ToLowerInvariant(),
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
            email.DeliveredAt);
    }
}
