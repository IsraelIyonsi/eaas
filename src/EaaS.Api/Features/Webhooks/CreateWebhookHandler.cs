using System.Security.Cryptography;
using EaaS.Domain.Entities;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Webhooks;

public sealed class CreateWebhookHandler : IRequestHandler<CreateWebhookCommand, WebhookCreatedDto>
{
    private readonly AppDbContext _dbContext;

    public CreateWebhookHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WebhookCreatedDto> Handle(CreateWebhookCommand request, CancellationToken cancellationToken)
    {
        // Check max 10 webhooks per tenant
        var count = await _dbContext.Webhooks
            .AsNoTracking()
            .CountAsync(w => w.TenantId == request.TenantId, cancellationToken);

        if (count >= WebhookConstants.MaxWebhooksPerTenant)
            throw new InvalidOperationException($"Maximum of {WebhookConstants.MaxWebhooksPerTenant} webhooks per tenant reached.");

        var secret = request.Secret ?? GenerateSecret();
        var now = DateTime.UtcNow;

        var webhook = new Webhook
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Url = request.Url,
            Events = request.Events.Select(e => e.ToLowerInvariant()).ToArray(),
            Secret = secret,
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Webhooks.Add(webhook);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new WebhookCreatedDto(
            webhook.Id,
            webhook.Url,
            webhook.Events,
            secret,
            webhook.Status,
            webhook.CreatedAt);
    }

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"{WebhookConstants.SecretPrefix}{Convert.ToBase64String(bytes)}";
    }
}
