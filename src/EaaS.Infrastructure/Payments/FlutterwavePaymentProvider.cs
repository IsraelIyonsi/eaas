using System.Globalization;
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
/// Flutterwave payment provider implementation.
/// Alternative African payment provider with broader currency support.
/// Uses Flutterwave v3 REST API.
/// </summary>
public sealed partial class FlutterwavePaymentProvider : IPaymentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentSettings _settings;
    private readonly ILogger<FlutterwavePaymentProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FlutterwavePaymentProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<PaymentSettings> options,
        ILogger<FlutterwavePaymentProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    public PaymentProvider ProviderType => PaymentProvider.Flutterwave;

    public Task<CreateCustomerResult> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CreateCustomer", request.Email);

        // Flutterwave doesn't have a dedicated customer API.
        // Customers are identified by email during payment initialization.
        var result = new CreateCustomerResult($"flw_cus_{Guid.NewGuid():N}", request.Email);
        return Task.FromResult(result);
    }

    public Task<bool> DeleteCustomerAsync(string externalCustomerId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "DeleteCustomer", externalCustomerId);

        // Flutterwave doesn't support customer deletion.
        // Local reference only - return true as no-op.
        return Task.FromResult(true);
    }

    public async Task<CreateSubscriptionResult> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CreateSubscription", request.ExternalCustomerId);

        var client = CreateClient();

        // Step 1: Create a payment plan
        var planBody = new
        {
            name = request.PlanExternalId,
            amount = 2999, // Default; in production this comes from plan config
            interval = "monthly",
            currency = "NGN"
        };

        var planResponse = await client.PostAsJsonAsync("payment-plans", planBody, JsonOptions, ct);
        var planJson = await planResponse.Content.ReadFromJsonAsync<FlutterwaveResponse<FlutterwavePlanData>>(JsonOptions, ct);

        EnsureSuccess(planResponse, planJson);

        // Step 2: Initiate payment with the plan attached
        var paymentBody = new
        {
            tx_ref = $"eaas_{Guid.NewGuid():N}",
            amount = planJson!.Data!.Amount,
            currency = "NGN",
            redirect_url = "https://app.example.com/billing/callback",
            payment_plan = planJson.Data.Id,
            customer = new { email = request.ExternalCustomerId }
        };

        var paymentResponse = await client.PostAsJsonAsync("payments", paymentBody, JsonOptions, ct);
        var paymentJson = await paymentResponse.Content.ReadFromJsonAsync<FlutterwaveResponse<FlutterwavePaymentData>>(JsonOptions, ct);

        EnsureSuccess(paymentResponse, paymentJson);

        return new CreateSubscriptionResult(
            planJson.Data.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "active",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            paymentJson!.Data!.Link);
    }

    public async Task<bool> CancelSubscriptionAsync(string externalSubscriptionId, bool immediate, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "CancelSubscription", externalSubscriptionId);

        var client = CreateClient();
        var response = await client.PutAsync($"subscriptions/{externalSubscriptionId}/cancel", null, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<SubscriptionInfo> GetSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "GetSubscription", externalSubscriptionId);

        var client = CreateClient();
        var response = await client.GetAsync($"subscriptions/{externalSubscriptionId}", ct);
        var json = await response.Content.ReadFromJsonAsync<FlutterwaveResponse<FlutterwaveSubscriptionData>>(JsonOptions, ct);

        EnsureSuccess(response, json);

        return new SubscriptionInfo(
            json!.Data!.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            json.Data.Status,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            null);
    }

    public async Task<InitiatePaymentResult> InitiatePaymentAsync(InitiatePaymentRequest request, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "InitiatePayment", request.ExternalCustomerId);

        var client = CreateClient();
        var txRef = $"eaas_{Guid.NewGuid():N}";

        var body = new
        {
            tx_ref = txRef,
            amount = request.AmountInMinorUnits,
            currency = request.Currency,
            redirect_url = request.CallbackUrl,
            customer = new { email = request.ExternalCustomerId },
            meta = request.Metadata ?? new Dictionary<string, string>()
        };

        var response = await client.PostAsJsonAsync("payments", body, JsonOptions, ct);
        var json = await response.Content.ReadFromJsonAsync<FlutterwaveResponse<FlutterwavePaymentData>>(JsonOptions, ct);

        EnsureSuccess(response, json);

        return new InitiatePaymentResult(
            txRef,
            json!.Data!.Link,
            "pending");
    }

    public async Task<bool> VerifyPaymentAsync(string externalPaymentId, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "VerifyPayment", externalPaymentId);

        var client = CreateClient();
        var response = await client.GetAsync($"transactions/{externalPaymentId}/verify", ct);
        var json = await response.Content.ReadFromJsonAsync<FlutterwaveResponse<FlutterwaveVerifyData>>(JsonOptions, ct);

        if (json is null || !string.Equals(json.Status, "success", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(json.Data?.Status, "successful", StringComparison.OrdinalIgnoreCase);
    }

    public Task<WebhookEvent?> ParseWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        LogProviderOperation(_logger, "ParseWebhook", "incoming");

        // Flutterwave uses verif-hash header compared against EncryptionKey
        var encryptionKey = _settings.Flutterwave?.EncryptionKey ?? string.Empty;

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(encryptionKey)))
        {
            LogWebhookSignatureFailure(_logger);
            return Task.FromResult<WebhookEvent?>(null);
        }

        var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var eventType = root.GetProperty("event").GetString() ?? string.Empty;
        var data = root.GetProperty("data");

        var externalId = data.TryGetProperty("tx_ref", out var txRefProp)
            ? txRefProp.GetString() ?? string.Empty
            : string.Empty;

        string? customerId = null;
        if (data.TryGetProperty("customer", out var customerProp) &&
            customerProp.TryGetProperty("email", out var emailProp))
        {
            customerId = emailProp.GetString();
        }

        string? subscriptionId = null;
        if (data.TryGetProperty("id", out var idProp))
        {
            subscriptionId = idProp.ToString();
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
        var client = _httpClientFactory.CreateClient("Flutterwave");

        // Ensure auth header is set if not already done by the factory
        if (!client.DefaultRequestHeaders.Contains("Authorization"))
        {
            var secretKey = _settings.Flutterwave?.SecretKey ?? string.Empty;
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {secretKey}");
        }

        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri("https://api.flutterwave.com/v3/");
        }

        return client;
    }

    private static void EnsureSuccess<T>(HttpResponseMessage response, FlutterwaveResponse<T>? json)
    {
        if (!response.IsSuccessStatusCode || json is null ||
            !string.Equals(json.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            var message = json?.Message ?? $"Flutterwave API error: HTTP {(int)response.StatusCode}";
            throw new FlutterwaveApiException(message, (int)response.StatusCode);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Flutterwave: {Operation} for {Identifier}")]
    private static partial void LogProviderOperation(ILogger logger, string operation, string identifier);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Flutterwave: Webhook signature verification failed")]
    private static partial void LogWebhookSignatureFailure(ILogger logger);

    // --- Flutterwave response models ---

    private sealed class FlutterwaveResponse<T>
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private sealed class FlutterwavePaymentData
    {
        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;
    }

    private sealed class FlutterwaveVerifyData
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("tx_ref")]
        public string TxRef { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;
    }

    private sealed class FlutterwavePlanData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("interval")]
        public string Interval { get; set; } = string.Empty;

        [JsonPropertyName("plan_token")]
        public string? PlanToken { get; set; }
    }

    private sealed class FlutterwaveSubscriptionData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}
