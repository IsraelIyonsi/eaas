using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Webhooks;

public sealed class GetWebhookHandler : IRequestHandler<GetWebhookQuery, WebhookDto>
{
    private readonly AppDbContext _dbContext;

    public GetWebhookHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WebhookDto> Handle(GetWebhookQuery request, CancellationToken cancellationToken)
    {
        var webhook = await _dbContext.Webhooks
            .AsNoTracking()
            .Where(w => w.Id == request.Id && w.TenantId == request.TenantId)
            .Select(w => new WebhookDto(
                w.Id,
                w.Url,
                w.Events,
                w.Status.ToString(),
                w.CreatedAt,
                w.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Webhook with id '{request.Id}' not found.");

        return webhook;
    }
}
