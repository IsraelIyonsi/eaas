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
/// PayStack payment provider implementation.
/// PayStack is the primary provider for Nigerian/African customers.
/// Uses PayStack REST API for customer, subscription, and payment management.
/// </summary>
public sealed partial class PayStackPaymentProvider : IPaymentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentSettings _settings;
    private readonly ILogger<PayStackPaymentProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PayStackPaymentProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<PaymentSettings> options,
        ILogger<PayStackPaymentProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    public PaymentProvider ProviderType => PaymentProvider.PayStack;

    public async Task<CreateCustomerResult> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CreateCustomer", request.Email);

        var client = CreateClient();

        var nameParts = request.Name.Split(' ', 2);
        var body = new
        {
            email = request.Email,
            first_name = nameParts[0],
            last_name = nameParts.Length > 1 ? nameParts[1] : string.Empty,
            metadata = request.Metadata ?? new Dictionary<string, string>()
        };

        var response = await client.PostAsJsonAsync("/customer", body, JsonOptions, ct);
        var json = await response.Content.ReadFromJsonAsync<PayStackResponse<PayStackCustomerData>>(JsonOptions, ct);

        EnsureSuccess(response, json);

        return new CreateCustomerResult(
            json!.Data!.CustomerCode,
            json.Data.Email);
    }

    public async Task<bool> DeleteCustomerAsync(string externalCustomerId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "DeleteCustomer", externalCustomerId);

        // PayStack doesn't support customer deletion directly.
        // Deactivate/blacklist instead via update endpoint.
        var client = CreateClient();
        var response = await client.PutAsJsonAsync(
            $"/customer/{externalCustomerId}",
            new { metadata = new { deleted = true } },
            JsonOptions, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<CreateSubscriptionResult> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CreateSubscription", request.ExternalCustomerId);

        var client = CreateClient();
        var body = new
        {
            customer = request.ExternalCustomerId,
            plan = request.PlanExternalId
        };

        var response = await client.PostAsJsonAsync("/subscription", body, JsonOptions, ct);
        var json = await response.Content.ReadFromJsonAsync<PayStackResponse<PayStackSubscriptionData>>(JsonOptions, ct);

        EnsureSuccess(response, json);

        var data = json!.Data!;
        var (periodStart, periodEnd) = ResolveSubscriptionPeriod(data);

        return new CreateSubscriptionResult(
            data.SubscriptionCode,
            data.Status,
            periodStart,
            periodEnd,
            null);
    }

    /// <summary>
    /// Resolves the current subscription billing period from PayStack's response.
    /// PayStack returns <c>next_payment_date</c> (end of current period) and often
    /// <c>createdAt</c> (start of current period). If the end date is missing or
    /// malformed, we log a warning and fall back to <c>UtcNow</c> + 30 days so the
    /// caller still receives a coherent period. If only the end date is available
    /// we use <c>UtcNow</c> as the start — this is a known limitation documented
    /// in PayStack's API where period interval is not always echoed back.
    /// </summary>
    private (DateTime Start, DateTime End) ResolveSubscriptionPeriod(PayStackSubscriptionData data)
    {
        var now = DateTime.UtcNow;

        if (!TryParseUtc(data.NextPaymentDate, out var end))
        {
            LogMissingPeriodDate(_logger, data.SubscriptionCode, data.NextPaymentDate ?? "<null>");
            return (now, now.AddDays(30));
        }

        var start = TryParseUtc(data.CreatedAt, out var parsedStart) ? parsedStart : now;
        return (start, end);
    }

    private static bool TryParseUtc(string? value, out DateTime parsed)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }

    public async Task<bool> CancelSubscriptionAsync(string externalSubscriptionId, bool immediate, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CancelSubscription", externalSubscriptionId);

        var client = CreateClient();
        var body = new { code = externalSubscriptionId, token = externalSubscriptionId };

        var response = await client.PostAsJsonAsync("/subscription/disable", body, JsonOptions, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<SubscriptionInfo> GetSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "GetSubscription", externalSubscriptionId);

        var client = CreateClient();
        var response = await client.GetAsync($"/subscription/{externalSubscriptionId}", ct);
        var json = await response.Content.ReadFromJsonAsync<PayStackResponse<PayStackSubscriptionData>>(JsonOptions, ct);

        EnsureSuccess(response, json);

        var data = json!.Data!;
        var (periodStart, periodEnd) = ResolveSubscriptionPeriod(data);

        return new SubscriptionInfo(
            data.SubscriptionCode,
            data.Status,
            periodStart,
            periodEnd,
            null);
    }

    public async Task<InitiatePaymentResult> InitiatePaymentAsync(InitiatePaymentRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "InitiatePayment", request.ExternalCustomerId);

        var client = CreateClient();
        var reference = $"eaas_{Guid.NewGuid():N}";

        var body = new
        {
            email = request.ExternalCustomerId, // PayStack uses email for transaction init
            amount = (int)request.AmountInMinorUnits,
            currency = request.Currency,
            callback_url = request.CallbackUrl,
            reference,
            metadata = request.Metadata ?? new Dictionary<string, string>()
        };

        var response = await client.PostAsJsonAsync("/transaction/initialize", body, JsonOptions, ct);
        var json = await response.Content.ReadFromJsonAsync<PayStackResponse<PayStackTransactionData>>(JsonOptions, ct);

        EnsureSuccess(response, json);

        return new InitiatePaymentResult(
            json!.Data!.Reference,
            json.Data.AuthorizationUrl,
            "pending");
    }

    public async Task<bool> VerifyPaymentAsync(string externalPaymentId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "VerifyPayment", externalPaymentId);

        var client = CreateClient();
        var response = await client.GetAsync($"/transaction/verify/{externalPaymentId}", ct);
        var json = await response.Content.ReadFromJsonAsync<PayStackResponse<PayStackVerifyData>>(JsonOptions, ct);

        if (json is null || !json.Status)
            return false;

        return string.Equals(json.Data?.Status, "success", StringComparison.OrdinalIgnoreCase);
    }

    public Task<WebhookEvent?> ParseWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "ParseWebhook", "incoming");

        var secretKey = _settings.PayStack?.SecretKey ?? string.Empty;

        // Verify HMAC-SHA512 signature
        var computedHash = ComputeHmacSha512(payload, secretKey);

        if (!string.Equals(computedHash, signature, StringComparison.OrdinalIgnoreCase))
        {
            LogWebhookSignatureFailure(_logger);
            return Task.FromResult<WebhookEvent?>(null);
        }

        var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var eventType = root.GetProperty("event").GetString() ?? string.Empty;
        var data = root.GetProperty("data");

        var externalId = data.TryGetProperty("reference", out var refProp)
            ? refProp.GetString() ?? string.Empty
            : string.Empty;

        string? customerId = null;
        if (data.TryGetProperty("customer", out var customerProp) &&
            customerProp.TryGetProperty("customer_code", out var codeProp))
        {
            customerId = codeProp.GetString();
        }

        string? subscriptionId = null;
        if (data.TryGetProperty("subscription_code", out var subCodeProp))
        {
            subscriptionId = subCodeProp.GetString();
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
        var client = _httpClientFactory.CreateClient("PayStack");

        // Ensure auth header is set if not already done by the factory
        if (!client.DefaultRequestHeaders.Contains("Authorization"))
        {
            var secretKey = _settings.PayStack?.SecretKey ?? string.Empty;
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {secretKey}");
        }

        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri("https://api.paystack.co");
        }

        return client;
    }

    private static void EnsureSuccess<T>(HttpResponseMessage response, PayStackResponse<T>? json)
    {
        if (!response.IsSuccessStatusCode || json is null || !json.Status)
        {
            var message = json?.Message ?? $"PayStack API error: HTTP {(int)response.StatusCode}";
            throw new PayStackApiException(message, (int)response.StatusCode);
        }
    }

    private static string ComputeHmacSha512(string payload, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "PayStack: {Operation} for {Identifier}")]
    private static partial void LogProviderOperation(ILogger logger, string operation, string identifier);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PayStack: Webhook signature verification failed")]
    private static partial void LogWebhookSignatureFailure(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PayStack: Subscription {SubscriptionCode} response missing or malformed next_payment_date ('{RawValue}'); falling back to UtcNow + 30 days")]
    private static partial void LogMissingPeriodDate(ILogger logger, string subscriptionCode, string rawValue);

    // --- PayStack response models ---

    private sealed class PayStackResponse<T>
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private sealed class PayStackCustomerData
    {
        [JsonPropertyName("customer_code")]
        public string CustomerCode { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private sealed class PayStackTransactionData
    {
        [JsonPropertyName("authorization_url")]
        public string AuthorizationUrl { get; set; } = string.Empty;

        [JsonPropertyName("reference")]
        public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("access_code")]
        public string AccessCode { get; set; } = string.Empty;
    }

    private sealed class PayStackVerifyData
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("reference")]
        public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public int Amount { get; set; }
    }

    private sealed class PayStackSubscriptionData
    {
        [JsonPropertyName("subscription_code")]
        public string SubscriptionCode { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("email_token")]
        public string? EmailToken { get; set; }

        /// <summary>
        /// ISO-8601 timestamp of the next scheduled charge — used as the end of the
        /// current billing period.
        /// </summary>
        [JsonPropertyName("next_payment_date")]
        public string? NextPaymentDate { get; set; }

        /// <summary>
        /// ISO-8601 subscription creation timestamp — used as the start of the
        /// current billing period when available.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }
    }
}
