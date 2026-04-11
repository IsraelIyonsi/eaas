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

public sealed class PayPalPaymentProviderTests
{
    private const string TestClientId = "pp_client_test_xxxxxxxxxxxxxxxxxxxx";
    private const string TestClientSecret = "pp_secret_test_xxxxxxxxxxxxxxxxxxxx";
    private const string TestWebhookId = "wh_test_xxxxxxxxxxxxxxxxxxxx";
    private const string FakeAccessToken = "A21AAF_fake_access_token_for_tests";

    private readonly IOptions<PaymentSettings> _options;
    private readonly ILogger<PayPalPaymentProvider> _logger;

    public PayPalPaymentProviderTests()
    {
        _options = Options.Create(new PaymentSettings
        {
            PayPal = new PayPalSettings
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret,
                UseSandbox = true,
                WebhookId = TestWebhookId
            }
        });
        _logger = Substitute.For<ILogger<PayPalPaymentProvider>>();
    }

    private PayPalPaymentProvider CreateSut(params (HttpStatusCode statusCode, object responseBody)[] responses)
    {
        var handler = new SequentialMockHttpMessageHandler(
            responses.Select(r => (r.statusCode, JsonSerializer.Serialize(r.responseBody))).ToList());
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api-m.sandbox.paypal.com/")
        };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("PayPal").Returns(httpClient);
        return new PayPalPaymentProvider(factory, _options, _logger);
    }

    [Fact]
    public async Task Should_CreateCustomer_ReturnEmail()
    {
        // PayPal has no customer create — use email as ID
        var sut = CreateSut(); // No HTTP calls needed

        var request = new CreateCustomerRequest("user@example.com", "John Doe", "Acme Corp");
        var result = await sut.CreateCustomerAsync(request);

        result.Should().NotBeNull();
        result.ExternalCustomerId.Should().Be("user@example.com");
        result.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task Should_InitiatePayment_ReturnApprovalUrl()
    {
        var sut = CreateSut(
            (HttpStatusCode.OK, new
            {
                access_token = FakeAccessToken,
                token_type = "Bearer",
                expires_in = 32400
            }),
            (HttpStatusCode.Created, new
            {
                id = "ORDER_5O190127TN364715T",
                status = "CREATED",
                links = new[]
                {
                    new { rel = "self", href = "https://api-m.sandbox.paypal.com/v2/checkout/orders/ORDER_5O190127TN364715T", method = "GET" },
                    new { rel = "approve", href = "https://www.sandbox.paypal.com/checkoutnow?token=ORDER_5O190127TN364715T", method = "GET" },
                    new { rel = "capture", href = "https://api-m.sandbox.paypal.com/v2/checkout/orders/ORDER_5O190127TN364715T/capture", method = "POST" }
                }
            }));

        var request = new InitiatePaymentRequest(
            "user@example.com",
            2999,
            "USD",
            "Pro Plan - Monthly",
            "https://app.example.com/billing/callback");

        var result = await sut.InitiatePaymentAsync(request);

        result.Should().NotBeNull();
        result.ExternalPaymentId.Should().Be("ORDER_5O190127TN364715T");
        result.PaymentUrl.Should().Contain("sandbox.paypal.com/checkoutnow");
        result.Status.Should().Be("CREATED");
    }

    [Fact]
    public async Task Should_VerifyPayment_ReturnTrue_WhenCompleted()
    {
        var sut = CreateSut(
            (HttpStatusCode.OK, new
            {
                access_token = FakeAccessToken,
                token_type = "Bearer",
                expires_in = 32400
            }),
            (HttpStatusCode.OK, new
            {
                id = "ORDER_5O190127TN364715T",
                status = "COMPLETED"
            }));

        var result = await sut.VerifyPaymentAsync("ORDER_5O190127TN364715T");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_VerifyPayment_ReturnFalse_WhenNotCompleted()
    {
        var sut = CreateSut(
            (HttpStatusCode.OK, new
            {
                access_token = FakeAccessToken,
                token_type = "Bearer",
                expires_in = 32400
            }),
            (HttpStatusCode.OK, new
            {
                id = "ORDER_5O190127TN364715T",
                status = "CREATED"
            }));

        var result = await sut.VerifyPaymentAsync("ORDER_5O190127TN364715T");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_CreateSubscription_ReturnSubscriptionId()
    {
        var sut = CreateSut(
            (HttpStatusCode.OK, new
            {
                access_token = FakeAccessToken,
                token_type = "Bearer",
                expires_in = 32400
            }),
            (HttpStatusCode.Created, new
            {
                id = "I-BW452GLLEP1G",
                status = "APPROVAL_PENDING",
                links = new[]
                {
                    new { rel = "approve", href = "https://www.sandbox.paypal.com/webapps/billing/subscriptions?ba_token=BA-123", method = "GET" },
                    new { rel = "self", href = "https://api-m.sandbox.paypal.com/v1/billing/subscriptions/I-BW452GLLEP1G", method = "GET" }
                }
            }));

        var request = new CreateSubscriptionRequest("user@example.com", "P-5ML4271244454362WXNWU5NQ");
        var result = await sut.CreateSubscriptionAsync(request);

        result.Should().NotBeNull();
        result.ExternalSubscriptionId.Should().Be("I-BW452GLLEP1G");
        result.Status.Should().Be("APPROVAL_PENDING");
        result.PaymentUrl.Should().Contain("sandbox.paypal.com");
    }

    [Fact]
    public async Task Should_CancelSubscription_Successfully()
    {
        var sut = CreateSut(
            (HttpStatusCode.OK, new
            {
                access_token = FakeAccessToken,
                token_type = "Bearer",
                expires_in = 32400
            }),
            (HttpStatusCode.NoContent, new { }));

        var result = await sut.CancelSubscriptionAsync("I-BW452GLLEP1G", immediate: true);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ParseWebhook_ValidEvent()
    {
        var webhookPayload = JsonSerializer.Serialize(new
        {
            id = "WH-7YX49823S2290830K-0JE13296W68552352",
            event_type = "PAYMENT.CAPTURE.COMPLETED",
            resource = new
            {
                id = "5O190127TN364715T",
                status = "COMPLETED",
                custom_id = "cus_test123"
            }
        });

        // Verification response
        var sut = CreateSut(
            (HttpStatusCode.OK, new
            {
                access_token = FakeAccessToken,
                token_type = "Bearer",
                expires_in = 32400
            }),
            (HttpStatusCode.OK, new
            {
                verification_status = "SUCCESS"
            }));

        var result = await sut.ParseWebhookAsync(webhookPayload, "valid-signature");

        result.Should().NotBeNull();
        result!.EventType.Should().Be("PAYMENT.CAPTURE.COMPLETED");
        result.ExternalId.Should().Be("5O190127TN364715T");
    }

    [Fact]
    public async Task Should_RejectWebhook_InvalidSignature()
    {
        var webhookPayload = JsonSerializer.Serialize(new
        {
            id = "WH-7YX49823S2290830K-0JE13296W68552352",
            event_type = "PAYMENT.CAPTURE.COMPLETED",
            resource = new
            {
                id = "5O190127TN364715T",
                status = "COMPLETED"
            }
        });

        var sut = CreateSut(
            (HttpStatusCode.OK, new
            {
                access_token = FakeAccessToken,
                token_type = "Bearer",
                expires_in = 32400
            }),
            (HttpStatusCode.OK, new
            {
                verification_status = "FAILURE"
            }));

        var result = await sut.ParseWebhookAsync(webhookPayload, "invalid-signature");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_GetSubscription_ReturnInfo()
    {
        var sut = CreateSut(
            (HttpStatusCode.OK, new
            {
                access_token = FakeAccessToken,
                token_type = "Bearer",
                expires_in = 32400
            }),
            (HttpStatusCode.OK, new
            {
                id = "I-BW452GLLEP1G",
                status = "ACTIVE",
                billing_info = new
                {
                    next_billing_time = "2026-05-01T00:00:00Z",
                    last_payment = new
                    {
                        amount = new { currency_code = "USD", value = "29.99" },
                        time = "2026-04-01T00:00:00Z"
                    }
                },
                start_time = "2026-04-01T00:00:00Z"
            }));

        var result = await sut.GetSubscriptionAsync("I-BW452GLLEP1G");

        result.Should().NotBeNull();
        result.ExternalSubscriptionId.Should().Be("I-BW452GLLEP1G");
        result.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task Should_DeleteCustomer_ReturnTrue()
    {
        // PayPal has no customer delete — always return true
        var sut = CreateSut();

        var result = await sut.DeleteCustomerAsync("user@example.com");

        result.Should().BeTrue();
    }

    [Fact]
    public void Should_HaveCorrectProviderType()
    {
        var sut = CreateSut();
        sut.ProviderType.Should().Be(PaymentProvider.PayPal);
    }
}

