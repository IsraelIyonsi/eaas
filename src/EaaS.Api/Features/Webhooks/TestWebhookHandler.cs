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
            ?? throw new KeyNotFoundException($"Webhook with id '{request.Id}' not found.");

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

        // Compute HMAC signature
        if (!string.IsNullOrWhiteSpace(webhook.Secret))
        {
            var keyBytes = Encoding.UTF8.GetBytes(webhook.Secret);
            var payloadBytes = Encoding.UTF8.GetBytes(json);
            var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
            var signature = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
            content.Headers.Add("X-EaaS-Signature", signature);
        }

        content.Headers.Add("X-EaaS-Event", "test");
        content.Headers.Add("X-EaaS-Delivery-Id", Guid.NewGuid().ToString());

        try
        {
            var client = _httpClientFactory.CreateClient("WebhookTest");
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
