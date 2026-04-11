using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Payments;

/// <summary>
/// Stripe payment provider implementation.
/// Uses Stripe REST API with HttpClient (form-encoded) for consistency with other providers.
/// </summary>
public sealed partial class StripePaymentProvider : IPaymentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentSettings _settings;
    private readonly ILogger<StripePaymentProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public StripePaymentProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<PaymentSettings> options,
        ILogger<StripePaymentProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    public PaymentProvider ProviderType => PaymentProvider.Stripe;

    public async Task<CreateCustomerResult> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CreateCustomer", request.Email);

        var client = CreateClient();

        var formData = new Dictionary<string, string>
        {
            ["email"] = request.Email,
            ["name"] = request.Name
        };

        if (request.Metadata is not null)
        {
            foreach (var (key, value) in request.Metadata)
            {
                formData[$"metadata[{key}]"] = value;
            }
        }

        var response = await client.PostAsync("customers", new FormUrlEncodedContent(formData), ct);
        var json = await response.Content.ReadFromJsonAsync<StripeCustomerResponse>(JsonOptions, ct);

        await EnsureSuccessAsync(response, json);

        return new CreateCustomerResult(
            json!.Id,
            json.Email ?? request.Email);
    }

    public async Task<bool> DeleteCustomerAsync(string externalCustomerId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "DeleteCustomer", externalCustomerId);

        var client = CreateClient();
        var response = await client.DeleteAsync($"customers/{externalCustomerId}", ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<CreateSubscriptionResult> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CreateSubscription", request.ExternalCustomerId);

        var client = CreateClient();

        var formData = new Dictionary<string, string>
        {
            ["customer"] = request.ExternalCustomerId,
            ["items[0][price]"] = request.PlanExternalId
        };

        if (!string.IsNullOrEmpty(request.CouponCode))
        {
            formData["coupon"] = request.CouponCode;
        }

        var response = await client.PostAsync("subscriptions", new FormUrlEncodedContent(formData), ct);
        var json = await response.Content.ReadFromJsonAsync<StripeSubscriptionResponse>(JsonOptions, ct);

        await EnsureSuccessAsync(response, json);

        return new CreateSubscriptionResult(
            json!.Id,
            json.Status,
            DateTimeOffset.FromUnixTimeSeconds(json.CurrentPeriodStart).UtcDateTime,
            DateTimeOffset.FromUnixTimeSeconds(json.CurrentPeriodEnd).UtcDateTime,
            null);
    }

    public async Task<bool> CancelSubscriptionAsync(string externalSubscriptionId, bool immediate, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CancelSubscription", externalSubscriptionId);

        var client = CreateClient();

        if (immediate)
        {
            var response = await client.DeleteAsync($"subscriptions/{externalSubscriptionId}", ct);
            return response.IsSuccessStatusCode;
        }

        // Cancel at period end: POST with cancel_at_period_end=true
        var formData = new Dictionary<string, string>
        {
            ["cancel_at_period_end"] = "true"
        };

        var updateResponse = await client.PostAsync(
            $"subscriptions/{externalSubscriptionId}",
            new FormUrlEncodedContent(formData), ct);

        return updateResponse.IsSuccessStatusCode;
    }

    public async Task<SubscriptionInfo> GetSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "GetSubscription", externalSubscriptionId);

        var client = CreateClient();
        var response = await client.GetAsync($"subscriptions/{externalSubscriptionId}", ct);
        var json = await response.Content.ReadFromJsonAsync<StripeSubscriptionResponse>(JsonOptions, ct);

        await EnsureSuccessAsync(response, json);

        return new SubscriptionInfo(
            json!.Id,
            json.Status,
            DateTimeOffset.FromUnixTimeSeconds(json.CurrentPeriodStart).UtcDateTime,
            DateTimeOffset.FromUnixTimeSeconds(json.CurrentPeriodEnd).UtcDateTime,
            json.CancelAtPeriodEnd ? "true" : null);
    }

    public async Task<InitiatePaymentResult> InitiatePaymentAsync(InitiatePaymentRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "InitiatePayment", request.ExternalCustomerId);

        var client = CreateClient();

        var formData = new Dictionary<string, string>
        {
            ["customer"] = request.ExternalCustomerId,
            ["line_items[0][price_data][currency]"] = request.Currency.ToLowerInvariant(),
            ["line_items[0][price_data][unit_amount]"] = ((int)request.AmountInMinorUnits).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = request.Description,
            ["line_items[0][quantity]"] = "1",
            ["mode"] = "payment",
            ["success_url"] = request.CallbackUrl,
            ["cancel_url"] = request.CallbackUrl
        };

        if (request.Metadata is not null)
        {
            foreach (var (key, value) in request.Metadata)
            {
                formData[$"metadata[{key}]"] = value;
            }
        }

        var response = await client.PostAsync("checkout/sessions", new FormUrlEncodedContent(formData), ct);
        var json = await response.Content.ReadFromJsonAsync<StripeCheckoutSessionResponse>(JsonOptions, ct);

        await EnsureSuccessAsync(response, json);

        return new InitiatePaymentResult(
            json!.Id,
            json.Url ?? string.Empty,
            "pending");
    }

    public async Task<bool> VerifyPaymentAsync(string externalPaymentId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "VerifyPayment", externalPaymentId);

        var client = CreateClient();
        var response = await client.GetAsync($"checkout/sessions/{externalPaymentId}", ct);
        var json = await response.Content.ReadFromJsonAsync<StripeCheckoutSessionResponse>(JsonOptions, ct);

        if (json is null)
            return false;

        return string.Equals(json.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
    }

    public Task<WebhookEvent?> ParseWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "ParseWebhook", "incoming");

        var webhookSecret = _settings.Stripe?.WebhookSecret ?? string.Empty;

        // Parse signature header: t=timestamp,v1=hash
        var parts = ParseSignatureHeader(signature);
        if (!parts.TryGetValue("t", out var timestamp) || !parts.TryGetValue("v1", out var v1Signature))
        {
            LogWebhookSignatureFailure(_logger);
            return Task.FromResult<WebhookEvent?>(null);
        }

        // Verify HMAC-SHA256 signature
        var signedPayload = $"{timestamp}.{payload}";
        var computedHash = ComputeHmacSha256(signedPayload, webhookSecret);

        if (!string.Equals(computedHash, v1Signature, StringComparison.OrdinalIgnoreCase))
        {
            LogWebhookSignatureFailure(_logger);
            return Task.FromResult<WebhookEvent?>(null);
        }

        var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var eventType = root.GetProperty("type").GetString() ?? string.Empty;
        var data = root.GetProperty("data").GetProperty("object");

        var externalId = data.TryGetProperty("id", out var idProp)
            ? idProp.GetString() ?? string.Empty
            : string.Empty;

        string? customerId = null;
        if (data.TryGetProperty("customer", out var customerProp))
        {
            customerId = customerProp.GetString();
        }

        string? subscriptionId = null;
        if (data.TryGetProperty("id", out var subIdProp) &&
            externalId.StartsWith("sub_", StringComparison.Ordinal))
        {
            subscriptionId = subIdProp.GetString();
        }

        // For invoice events, the subscription is a separate field
        if (subscriptionId is null && data.TryGetProperty("subscription", out var subProp) &&
            subProp.ValueKind == JsonValueKind.String)
        {
            subscriptionId = subProp.GetString();
        }

        var eventData = new Dictionary<string, object>();
        foreach (var prop in data.EnumerateObject())
        {
            eventData[prop.Name] = prop.Value.ToString();
        }

        var webhookEvent = new WebhookEvent(
            eventType,
            externalId,
            customerId,
            subscriptionId,
            eventData);

        return Task.FromResult<WebhookEvent?>(webhookEvent);
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("Stripe");

        if (!client.DefaultRequestHeaders.Contains("Authorization"))
        {
            var secretKey = _settings.Stripe?.SecretKey ?? string.Empty;
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {secretKey}");
        }

        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri("https://api.stripe.com/v1/");
        }

        return client;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, object? json)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Try to extract Stripe error message
        var message = $"Stripe API error: HTTP {(int)response.StatusCode}";
        string? errorType = null;

        try
        {
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("error", out var errorObj))
            {
                if (errorObj.TryGetProperty("message", out var msgProp))
                    message = msgProp.GetString() ?? message;
                if (errorObj.TryGetProperty("type", out var typeProp))
                    errorType = typeProp.GetString();
            }
        }
        catch
        {
            // Use default message if parsing fails
        }

        throw new StripeApiException(message, (int)response.StatusCode, errorType);
    }

    private static Dictionary<string, string> ParseSignatureHeader(string header)
    {
        var result = new Dictionary<string, string>();
        foreach (var part in header.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
            {
                result[kv[0]] = kv[1];
            }
        }
        return result;
    }

    private static string ComputeHmacSha256(string payload, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Stripe: {Operation} for {Identifier}")]
    private static partial void LogProviderOperation(ILogger logger, string operation, string identifier);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe: Webhook signature verification failed")]
    private static partial void LogWebhookSignatureFailure(ILogger logger);

    // --- Stripe response models ---

    private sealed class StripeCustomerResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("error")]
        public StripeErrorResponse? Error { get; set; }
    }

    private sealed class StripeSubscriptionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("current_period_start")]
        public long CurrentPeriodStart { get; set; }

        [JsonPropertyName("current_period_end")]
        public long CurrentPeriodEnd { get; set; }

        [JsonPropertyName("cancel_at_period_end")]
        public bool CancelAtPeriodEnd { get; set; }

        [JsonPropertyName("error")]
        public StripeErrorResponse? Error { get; set; }
    }

    private sealed class StripeCheckoutSessionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("payment_status")]
        public string PaymentStatus { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public StripeErrorResponse? Error { get; set; }
    }

    private sealed class StripeErrorResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}
