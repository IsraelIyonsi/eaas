using EaaS.Api.Constants;
using EaaS.Domain.Exceptions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Webhooks;

public sealed class TestWebhookHandler : IRequestHandler<TestWebhookCommand, TestWebhookResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public TestWebhookHandler(AppDbContext dbContext, IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TestWebhookResult> Handle(TestWebhookCommand request, CancellationToken cancellationToken)
    {
        var webhook = await _dbContext.Webhooks
            .AsNoTracking()
            .Where(w => w.Id == request.Id && w.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Webhook with id '{request.Id}' not found.");

        var testPayload = new
        {
            @event = "test",
            message_id = "test_" + Guid.NewGuid().ToString("N")[..12],
            email_id = Guid.Empty,
            timestamp = DateTime.UtcNow,
            data = new { message = "This is a test webhook delivery from EaaS." }
        };

        var json = JsonSerializer.Serialize(testPayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        EaaS.Shared.Utilities.WebhookSigner.ApplyHeaders(content, webhook.Secret, json, "test", Guid.NewGuid().ToString());

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientNameConstants.WebhookTest);
            client.Timeout = TimeSpan.FromSeconds(WebhookConstants.TestTimeoutSeconds);

            var response = await client.PostAsync(webhook.Url, content, cancellationToken);
            return new TestWebhookResult(response.IsSuccessStatusCode, (int)response.StatusCode, null);
        }
        catch (Exception ex)
        {
            return new TestWebhookResult(false, 0, ex.Message);
        }
    }
}
