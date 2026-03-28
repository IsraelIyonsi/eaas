using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Webhooks;

public sealed class UpdateWebhookHandler : IRequestHandler<UpdateWebhookCommand, WebhookDto>
{
    private readonly AppDbContext _dbContext;

    public UpdateWebhookHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WebhookDto> Handle(UpdateWebhookCommand request, CancellationToken cancellationToken)
    {
        var webhook = await _dbContext.Webhooks
            .Where(w => w.Id == request.Id && w.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Webhook with id '{request.Id}' not found.");

        if (request.Url is not null)
            webhook.Url = request.Url;

        if (request.Events is not null)
            webhook.Events = request.Events.Select(e => e.ToLowerInvariant()).ToArray();

        if (request.Status is not null)
            webhook.Status = request.Status;

        webhook.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new WebhookDto(
            webhook.Id,
            webhook.Url,
            webhook.Events,
            webhook.Status,
            webhook.CreatedAt,
            webhook.UpdatedAt);
    }
}
