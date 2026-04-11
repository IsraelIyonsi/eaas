using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Payments;

/// <summary>
/// PayPal payment provider implementation.
/// Uses PayPal REST API v2 for orders and v1 for subscriptions/billing.
/// OAuth2 client credentials flow for authentication with token caching.
/// </summary>
public sealed partial class PayPalPaymentProvider : IPaymentProvider, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentSettings _settings;
    private readonly ILogger<PayPalPaymentProvider> _logger;

    private string? _cachedAccessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PayPalPaymentProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<PaymentSettings> options,
        ILogger<PayPalPaymentProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    public PaymentProvider ProviderType => PaymentProvider.PayPal;

    public Task<CreateCustomerResult> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CreateCustomer", request.Email);

        // PayPal doesn't have a customer create API — customers are PayPal account holders.
        // Use email as the external ID.
        return Task.FromResult(new CreateCustomerResult(request.Email, request.Email));
    }

    public Task<bool> DeleteCustomerAsync(string externalCustomerId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "DeleteCustomer", externalCustomerId);

        // PayPal doesn't have a customer delete API — no-op, return true.
        return Task.FromResult(true);
    }

    public async Task<InitiatePaymentResult> InitiatePaymentAsync(InitiatePaymentRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "InitiatePayment", request.ExternalCustomerId);

        var client = await CreateAuthenticatedClientAsync(ct);

        var orderRequest = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    amount = new
                    {
                        currency_code = request.Currency.ToUpperInvariant(),
                        value = (request.AmountInMinorUnits / 100m).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    },
                    description = request.Description
                }
            },
            application_context = new
            {
                return_url = request.CallbackUrl,
                cancel_url = request.CallbackUrl
            }
        };

        var response = await client.PostAsJsonAsync("v2/checkout/orders", orderRequest, JsonOptions, ct);
        var json = await response.Content.ReadFromJsonAsync<PayPalOrderResponse>(JsonOptions, ct);

        await EnsureSuccessAsync(response, json?.Name);

        var approveLink = json!.Links?.FirstOrDefault(l =>
            string.Equals(l.Rel, "approve", StringComparison.OrdinalIgnoreCase));

        return new InitiatePaymentResult(
            json.Id,
            approveLink?.Href ?? string.Empty,
            json.Status);
    }

    public async Task<bool> VerifyPaymentAsync(string externalPaymentId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "VerifyPayment", externalPaymentId);

        var client = await CreateAuthenticatedClientAsync(ct);

        var response = await client.GetAsync($"v2/checkout/orders/{externalPaymentId}", ct);
        var json = await response.Content.ReadFromJsonAsync<PayPalOrderResponse>(JsonOptions, ct);

        if (json is null)
            return false;

        return string.Equals(json.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CreateSubscriptionResult> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CreateSubscription", request.ExternalCustomerId);

        var client = await CreateAuthenticatedClientAsync(ct);

        var subRequest = new
        {
            plan_id = request.PlanExternalId,
            subscriber = new
            {
                email_address = request.ExternalCustomerId
            },
            application_context = new
            {
                return_url = "https://app.example.com/billing/success",
                cancel_url = "https://app.example.com/billing/cancel"
            }
        };

        var response = await client.PostAsJsonAsync("v1/billing/subscriptions", subRequest, JsonOptions, ct);
        var json = await response.Content.ReadFromJsonAsync<PayPalSubscriptionResponse>(JsonOptions, ct);

        await EnsureSuccessAsync(response, json?.Name);

        var approveLink = json!.Links?.FirstOrDefault(l =>
            string.Equals(l.Rel, "approve", StringComparison.OrdinalIgnoreCase));

        return new CreateSubscriptionResult(
            json.Id,
            json.Status,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            approveLink?.Href);
    }

    public async Task<bool> CancelSubscriptionAsync(string externalSubscriptionId, bool immediate, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CancelSubscription", externalSubscriptionId);

        var client = await CreateAuthenticatedClientAsync(ct);

        var cancelRequest = new { reason = "Customer requested cancellation" };
        var response = await client.PostAsJsonAsync(
            $"v1/billing/subscriptions/{externalSubscriptionId}/cancel",
            cancelRequest, JsonOptions, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<SubscriptionInfo> GetSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "GetSubscription", externalSubscriptionId);

        var client = await CreateAuthenticatedClientAsync(ct);

        var response = await client.GetAsync($"v1/billing/subscriptions/{externalSubscriptionId}", ct);
        var json = await response.Content.ReadFromJsonAsync<PayPalSubscriptionResponse>(JsonOptions, ct);

        await EnsureSuccessAsync(response, json?.Name);

        var nextBilling = DateTime.UtcNow.AddMonths(1);
        var startTime = DateTime.UtcNow;

        if (json!.BillingInfo?.NextBillingTime is not null &&
            DateTime.TryParse(json.BillingInfo.NextBillingTime, out var parsed))
        {
            nextBilling = parsed.ToUniversalTime();
        }

        if (json.StartTime is not null && DateTime.TryParse(json.StartTime, out var startParsed))
        {
            startTime = startParsed.ToUniversalTime();
        }

        var cancelAtPeriodEnd = string.Equals(json.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)
            ? "true"
            : null;

        return new SubscriptionInfo(
            json.Id,
            json.Status,
            startTime,
            nextBilling,
            cancelAtPeriodEnd);
    }

    public async Task<WebhookEvent?> ParseWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "ParseWebhook", "incoming");

        var webhookId = _settings.PayPal?.WebhookId ?? string.Empty;

        // Verify webhook via PayPal API
        var client = await CreateAuthenticatedClientAsync(ct);

        var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var verifyRequest = new
        {
            webhook_id = webhookId,
            webhook_event = JsonSerializer.Deserialize<object>(payload, JsonOptions)
        };

        var verifyResponse = await client.PostAsJsonAsync(
            "v1/notifications/verify-webhook-signature",
            verifyRequest, JsonOptions, ct);

        var verifyJson = await verifyResponse.Content.ReadFromJsonAsync<PayPalWebhookVerifyResponse>(JsonOptions, ct);

        if (verifyJson is null ||
            !string.Equals(verifyJson.VerificationStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            LogWebhookSignatureFailure(_logger);
            return null;
        }

        var eventType = root.GetProperty("event_type").GetString() ?? string.Empty;

        var resource = root.GetProperty("resource");
        var externalId = resource.TryGetProperty("id", out var idProp)
            ? idProp.GetString() ?? string.Empty
            : string.Empty;

        string? customerId = null;
        if (resource.TryGetProperty("custom_id", out var customerProp))
        {
            customerId = customerProp.GetString();
        }

        string? subscriptionId = null;
        if (resource.TryGetProperty("billing_agreement_id", out var subProp))
        {
            subscriptionId = subProp.GetString();
        }
        else if (externalId.StartsWith("I-", StringComparison.Ordinal))
        {
            subscriptionId = externalId;
        }

        var eventData = new Dictionary<string, object>();
        foreach (var prop in resource.EnumerateObject())
        {
            eventData[prop.Name] = prop.Value.ToString();
        }

        return new WebhookEvent(
            eventType,
            externalId,
            customerId,
            subscriptionId,
            eventData);
    }

    public void Dispose() => _tokenLock.Dispose();

    // --- Internal Helpers ---

    private async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("PayPal");

        if (client.BaseAddress is null)
        {
            var baseUrl = _settings.PayPal?.UseSandbox == true
                ? "https://api-m.sandbox.paypal.com"
                : "https://api-m.paypal.com";
            client.BaseAddress = new Uri(baseUrl + "/");
        }

        var accessToken = await GetAccessTokenAsync(client, ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    private async Task<string> GetAccessTokenAsync(HttpClient client, CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedAccessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _cachedAccessToken;

            var clientId = _settings.PayPal?.ClientId ?? string.Empty;
            var clientSecret = _settings.PayPal?.ClientSecret ?? string.Empty;
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials"
                })
            };
            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await client.SendAsync(tokenRequest, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<PayPalTokenResponse>(JsonOptions, ct);

            _cachedAccessToken = json!.AccessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(json.ExpiresIn - 60); // 60s buffer

            return _cachedAccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string? errorName)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = $"PayPal API error: HTTP {(int)response.StatusCode}";
        string? errorType = errorName;

        try
        {
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("message", out var msgProp))
                message = msgProp.GetString() ?? message;
            if (doc.RootElement.TryGetProperty("name", out var nameProp))
                errorType = nameProp.GetString();
        }
        catch
        {
            // Use default message if parsing fails
        }

        throw new PayPalApiException(message, (int)response.StatusCode, errorType);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "PayPal: {Operation} for {Identifier}")]
    private static partial void LogProviderOperation(ILogger logger, string operation, string identifier);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PayPal: Webhook signature verification failed")]
    private static partial void LogWebhookSignatureFailure(ILogger logger);

    // --- PayPal response models ---

    private sealed class PayPalTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class PayPalOrderResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("links")]
        public PayPalLink[]? Links { get; set; }
    }

    private sealed class PayPalSubscriptionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("billing_info")]
        public PayPalBillingInfo? BillingInfo { get; set; }

        [JsonPropertyName("links")]
        public PayPalLink[]? Links { get; set; }
    }

    private sealed class PayPalBillingInfo
    {
        [JsonPropertyName("next_billing_time")]
        public string? NextBillingTime { get; set; }
    }

    private sealed class PayPalLink
    {
        [JsonPropertyName("rel")]
        public string Rel { get; set; } = string.Empty;

        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;

        [JsonPropertyName("method")]
        public string? Method { get; set; }
    }

    private sealed class PayPalWebhookVerifyResponse
    {
        [JsonPropertyName("verification_status")]
        public string VerificationStatus { get; set; } = string.Empty;
    }
}
