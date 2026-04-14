using EaaS.Api.Constants;
using EaaS.Domain.Exceptions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using EaaS.Shared.Utilities;
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

        // Defence in depth against Finding C3: re-validate persisted URL before
        // issuing the test request.
        if (!SsrfGuard.IsSyntacticallySafe(webhook.Url, out var ssrfReason))
        {
            SsrfGuard.RecordSsrfRejection("syntactic");
            return new TestWebhookResult(false, 0,
                "We could not reach this URL because it resolves to a restricted or private network.");
        }

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

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, webhook.Url) { Content = content };
            using var response = await client.SendAsync(
                requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // Drain up to 64 KB of the body with truncation (customer may legitimately return
            // a larger response, but we only need the prefix for diagnostics).
            _ = await SsrfGuard.ReadBoundedStringAsync(
                response, truncate: true, cancellationToken: cancellationToken);

            return new TestWebhookResult(response.IsSuccessStatusCode, (int)response.StatusCode, null);
        }
        catch (HttpRequestException ex) when (
            ex.Message.Contains("blocked range", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("did not resolve", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("SSRF", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("refused by SSRF", StringComparison.OrdinalIgnoreCase))
        {
            // Map guard-level refusals to a customer-friendly message. Do NOT leak the raw
            // reason (which includes the resolved IP — a minor info disclosure).
            SsrfGuard.RecordSsrfRejection("connect_guard");
            return new TestWebhookResult(false, 0,
                "We could not reach this URL because it resolves to a restricted or private network.");
        }
        catch (Exception ex)
        {
            return new TestWebhookResult(false, 0, ex.Message);
        }
    }
}
