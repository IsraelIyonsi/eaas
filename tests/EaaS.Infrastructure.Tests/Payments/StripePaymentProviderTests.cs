using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EaaS.Infrastructure.Tests.Payments;

public sealed class StripePaymentProviderTests
{
    private const string TestSecretKey = "sk_test_xxxxxxxxxxxxxxxxxxxx";
    private const string TestWebhookSecret = "whsec_test_xxxxxxxxxxxxxxxxxxxx";

    private readonly IOptions<PaymentSettings> _options;
    private readonly ILogger<StripePaymentProvider> _logger;

    public StripePaymentProviderTests()
    {
        _options = Options.Create(new PaymentSettings
        {
            Stripe = new StripeSettings
            {
                SecretKey = TestSecretKey,
                PublishableKey = "pk_test_xxxxxxxxxxxxxxxxxxxx",
                WebhookSecret = TestWebhookSecret
            }
        });
        _logger = Substitute.For<ILogger<StripePaymentProvider>>();
    }

    private StripePaymentProvider CreateSut(HttpClient httpClient)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Stripe").Returns(httpClient);
        return new StripePaymentProvider(factory, _options, _logger);
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, object responseBody)
    {
        var handler = new MockHttpMessageHandler(statusCode, JsonSerializer.Serialize(responseBody));
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.stripe.com/v1/")
        };
    }

    [Fact]
    public async Task Should_CreateCustomer_Successfully()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            id = "cus_test123",
            @object = "customer",
            email = "user@example.com",
            name = "John Doe"
        });

        var sut = CreateSut(httpClient);
        var request = new CreateCustomerRequest("user@example.com", "John Doe", "Acme Corp");

        var result = await sut.CreateCustomerAsync(request);

        result.Should().NotBeNull();
        result.ExternalCustomerId.Should().Be("cus_test123");
        result.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task Should_ThrowWhenApiReturns400()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.BadRequest, new
        {
            error = new
            {
                message = "Invalid email address",
                type = "invalid_request_error"
            }
        });

        var sut = CreateSut(httpClient);
        var request = new CreateCustomerRequest("invalid", "John", null);

        var act = () => sut.CreateCustomerAsync(request);

        await act.Should().ThrowAsync<StripeApiException>()
            .WithMessage("*Invalid email address*");
    }

    [Fact]
    public async Task Should_InitiatePayment_ReturnCheckoutUrl()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            id = "cs_test_abc123",
            @object = "checkout.session",
            url = "https://checkout.stripe.com/c/pay/cs_test_abc123"
        });

        var sut = CreateSut(httpClient);
        var request = new InitiatePaymentRequest(
            "cus_test123",
            2999,
            "USD",
            "Pro Plan - Monthly",
            "https://app.example.com/billing/callback");

        var result = await sut.InitiatePaymentAsync(request);

        result.Should().NotBeNull();
        result.PaymentUrl.Should().Be("https://checkout.stripe.com/c/pay/cs_test_abc123");
        result.ExternalPaymentId.Should().Be("cs_test_abc123");
        result.Status.Should().Be("pending");
    }

    [Fact]
    public async Task Should_VerifyPayment_ReturnTrue_WhenSuccessful()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            id = "cs_test_abc123",
            @object = "checkout.session",
            payment_status = "paid"
        });

        var sut = CreateSut(httpClient);
        var result = await sut.VerifyPaymentAsync("cs_test_abc123");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_VerifyPayment_ReturnFalse_WhenFailed()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            id = "cs_test_abc123",
            @object = "checkout.session",
            payment_status = "unpaid"
        });

        var sut = CreateSut(httpClient);
        var result = await sut.VerifyPaymentAsync("cs_test_abc123");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ParseWebhook_ValidSignature()
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_test123",
            type = "customer.subscription.created",
            data = new
            {
                @object = new
                {
                    id = "sub_test123",
                    customer = "cus_test123",
                    status = "active"
                }
            }
        });

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signedPayload = $"{timestamp}.{payload}";
        var v1Signature = ComputeHmacSha256(signedPayload, TestWebhookSecret);
        var signature = $"t={timestamp},v1={v1Signature}";

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new { }); // Not used for webhooks
        var sut = CreateSut(httpClient);

        var result = await sut.ParseWebhookAsync(payload, signature);

        result.Should().NotBeNull();
        result!.EventType.Should().Be("customer.subscription.created");
        result.ExternalSubscriptionId.Should().Be("sub_test123");
        result.ExternalCustomerId.Should().Be("cus_test123");
    }

    [Fact]
    public async Task Should_RejectWebhook_InvalidSignature()
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_test123",
            type = "customer.subscription.created",
            data = new
            {
                @object = new
                {
                    id = "sub_test123",
                    customer = "cus_test123"
                }
            }
        });

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new { });
        var sut = CreateSut(httpClient);

        var result = await sut.ParseWebhookAsync(payload, "t=123,v1=invalid_signature");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_CreateSubscription_Successfully()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            id = "sub_test123",
            @object = "subscription",
            status = "active",
            current_period_start = 1700000000,
            current_period_end = 1702592000
        });

        var sut = CreateSut(httpClient);
        var request = new CreateSubscriptionRequest("cus_test123", "price_test123");

        var result = await sut.CreateSubscriptionAsync(request);

        result.Should().NotBeNull();
        result.ExternalSubscriptionId.Should().Be("sub_test123");
        result.Status.Should().Be("active");
        result.CurrentPeriodStart.Should().BeAfter(DateTime.MinValue);
        result.CurrentPeriodEnd.Should().BeAfter(result.CurrentPeriodStart);
    }

    [Fact]
    public async Task Should_CancelSubscription_AtPeriodEnd()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            id = "sub_test123",
            @object = "subscription",
            cancel_at_period_end = true
        });

        var sut = CreateSut(httpClient);
        var result = await sut.CancelSubscriptionAsync("sub_test123", immediate: false);

        result.Should().BeTrue();
    }

    private static string ComputeHmacSha256(string payload, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexStringLower(hash);
    }
}
