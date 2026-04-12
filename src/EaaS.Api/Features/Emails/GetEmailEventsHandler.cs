using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Emails;

public sealed class GetEmailEventsHandler : IRequestHandler<GetEmailEventsQuery, List<EmailEventDto>>
{
    private readonly AppDbContext _dbContext;

    public GetEmailEventsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<EmailEventDto>> Handle(GetEmailEventsQuery request, CancellationToken cancellationToken)
    {
        var emailExists = await _dbContext.Emails
            .AsNoTracking()
            .AnyAsync(e => e.Id == request.EmailId && e.TenantId == request.TenantId, cancellationToken);

        if (!emailExists)
            throw new NotFoundException($"Email with id '{request.EmailId}' not found.");

        return await _dbContext.EmailEvents
            .AsNoTracking()
            .Where(ev => ev.EmailId == request.EmailId)
            .OrderBy(ev => ev.CreatedAt)
            .Select(ev => new EmailEventDto(
                ev.Id,
                ev.EventType.ToString().ToLowerInvariant(),
                ev.Data,
                ev.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
