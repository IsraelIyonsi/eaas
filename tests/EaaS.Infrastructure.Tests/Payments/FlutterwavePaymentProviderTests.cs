using System.Net;
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

public sealed class FlutterwavePaymentProviderTests
{
    private const string TestSecretKey = "FLWSECK_TEST-xxxxxxxxxxxxxxxxxxxx";
    private const string TestEncryptionKey = "FLWSECK_TESTenckey";

    private readonly IOptions<PaymentSettings> _options;
    private readonly ILogger<FlutterwavePaymentProvider> _logger;

    public FlutterwavePaymentProviderTests()
    {
        _options = Options.Create(new PaymentSettings
        {
            Flutterwave = new FlutterwaveSettings
            {
                SecretKey = TestSecretKey,
                PublicKey = "FLWPUBK_TEST-xxxxxxxxxxxxxxxxxxxx",
                EncryptionKey = TestEncryptionKey
            }
        });
        _logger = Substitute.For<ILogger<FlutterwavePaymentProvider>>();
    }

    private FlutterwavePaymentProvider CreateSut(HttpClient httpClient)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Flutterwave").Returns(httpClient);
        return new FlutterwavePaymentProvider(factory, _options, _logger);
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, object responseBody)
    {
        var handler = new MockHttpMessageHandler(statusCode, JsonSerializer.Serialize(responseBody));
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.flutterwave.com/v3/")
        };
    }

    [Fact]
    public async Task Should_CreateCustomer_Successfully()
    {
        // Flutterwave doesn't have a customer API - it creates a local reference
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new { }); // Not used
        var sut = CreateSut(httpClient);
        var request = new CreateCustomerRequest("user@example.com", "John Doe", "Acme Corp");

        var result = await sut.CreateCustomerAsync(request);

        result.Should().NotBeNull();
        result.ExternalCustomerId.Should().StartWith("flw_cus_");
        result.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task Should_InitiatePayment_ReturnPaymentLink()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = "success",
            message = "Hosted Link",
            data = new
            {
                link = "https://checkout.flutterwave.com/v3/hosted/pay/flw_test_abc123"
            }
        });

        var sut = CreateSut(httpClient);
        var request = new InitiatePaymentRequest(
            "user@example.com",
            2999,
            "NGN",
            "Pro Plan - Monthly",
            "https://app.example.com/billing/callback");

        var result = await sut.InitiatePaymentAsync(request);

        result.Should().NotBeNull();
        result.PaymentUrl.Should().Be("https://checkout.flutterwave.com/v3/hosted/pay/flw_test_abc123");
        result.ExternalPaymentId.Should().StartWith("eaas_");
        result.Status.Should().Be("pending");
    }

    [Fact]
    public async Task Should_VerifyPayment_ReturnTrue_WhenSuccessful()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = "success",
            data = new
            {
                status = "successful",
                tx_ref = "eaas_test123",
                amount = 2999,
                currency = "NGN"
            }
        });

        var sut = CreateSut(httpClient);
        var result = await sut.VerifyPaymentAsync("12345");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_VerifyPayment_ReturnFalse_WhenFailed()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = "success",
            data = new
            {
                status = "failed",
                tx_ref = "eaas_test123"
            }
        });

        var sut = CreateSut(httpClient);
        var result = await sut.VerifyPaymentAsync("12345");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ParseWebhook_ValidHash()
    {
        var payload = JsonSerializer.Serialize(new
        {
            @event = "charge.completed",
            data = new
            {
                tx_ref = "eaas_test123",
                flw_ref = "FLW-MOCK-abc123",
                customer = new { email = "user@example.com" },
                status = "successful"
            }
        });

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new { }); // Not used for webhooks
        var sut = CreateSut(httpClient);

        var result = await sut.ParseWebhookAsync(payload, TestEncryptionKey);

        result.Should().NotBeNull();
        result!.EventType.Should().Be("charge.completed");
        result.ExternalId.Should().Be("eaas_test123");
        result.ExternalCustomerId.Should().Be("user@example.com");
    }

    [Fact]
    public async Task Should_RejectWebhook_InvalidHash()
    {
        var payload = JsonSerializer.Serialize(new
        {
            @event = "charge.completed",
            data = new { tx_ref = "eaas_test123" }
        });

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new { });
        var sut = CreateSut(httpClient);

        var result = await sut.ParseWebhookAsync(payload, "wrong_hash_value");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_CancelSubscription_Successfully()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = "success",
            message = "Subscription cancelled"
        });

        var sut = CreateSut(httpClient);
        var result = await sut.CancelSubscriptionAsync("sub_12345", immediate: true);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_CreateSubscription_Successfully()
    {
        // Flutterwave subscription = create payment plan, then initiate payment with plan
        var handler = new SequentialMockHttpMessageHandler(
        [
            // First call: POST /payment-plans
            new(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    id = 54321,
                    name = "Pro Plan",
                    amount = 2999,
                    interval = "monthly",
                    plan_token = "rpp_test_abc123"
                }
            })),
            // Second call: POST /payments (initiate payment with plan)
            new(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    link = "https://checkout.flutterwave.com/v3/hosted/pay/flw_sub_abc123"
                }
            }))
        ]);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.flutterwave.com/v3/")
        };

        var sut = CreateSut(httpClient);
        var request = new CreateSubscriptionRequest("user@example.com", "Pro Plan");

        var result = await sut.CreateSubscriptionAsync(request);

        result.Should().NotBeNull();
        result.ExternalSubscriptionId.Should().Be("54321");
        result.Status.Should().Be("active");
        result.PaymentUrl.Should().Be("https://checkout.flutterwave.com/v3/hosted/pay/flw_sub_abc123");
    }

    [Fact]
    public async Task Should_GetSubscription_Successfully()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = "success",
            data = new
            {
                id = 54321,
                status = "active",
                plan = new { id = 100, name = "Pro Plan", amount = 2999 },
                customer = new { customer_email = "user@example.com" },
                created_at = "2026-01-01T00:00:00Z"
            }
        });

        var sut = CreateSut(httpClient);
        var result = await sut.GetSubscriptionAsync("54321");

        result.Should().NotBeNull();
        result.ExternalSubscriptionId.Should().Be("54321");
        result.Status.Should().Be("active");
    }

    [Fact]
    public void ProviderType_ShouldBeFlutterwave()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new { });
        var sut = CreateSut(httpClient);

        sut.ProviderType.Should().Be(PaymentProvider.Flutterwave);
    }

    [Fact]
    public async Task Should_ThrowFlutterwaveApiException_OnErrorResponse()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.BadRequest, new
        {
            status = "error",
            message = "Invalid payment request"
        });

        var sut = CreateSut(httpClient);
        var request = new InitiatePaymentRequest(
            "user@example.com",
            0, // invalid amount
            "NGN",
            "Test",
            "https://app.example.com/callback");

        var act = () => sut.InitiatePaymentAsync(request);

        await act.Should().ThrowAsync<FlutterwaveApiException>()
            .WithMessage("*Invalid payment request*");
    }
}
