using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Webhooks;

public sealed class DeleteWebhookHandler : IRequestHandler<DeleteWebhookCommand>
{
    private readonly AppDbContext _dbContext;

    public DeleteWebhookHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteWebhookCommand request, CancellationToken cancellationToken)
    {
        var webhook = await _dbContext.Webhooks
            .Where(w => w.Id == request.Id && w.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Webhook with id '{request.Id}' not found.");

        _dbContext.Webhooks.Remove(webhook);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
