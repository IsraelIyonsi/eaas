using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
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

public sealed class PayStackPaymentProviderTests
{
    private const string TestSecretKey = "sk_test_xxxxxxxxxxxxxxxxxxxx";

    private readonly IOptions<PaymentSettings> _options;
    private readonly ILogger<PayStackPaymentProvider> _logger;

    public PayStackPaymentProviderTests()
    {
        _options = Options.Create(new PaymentSettings
        {
            PayStack = new PayStackSettings
            {
                SecretKey = TestSecretKey,
                PublicKey = "pk_test_xxxxxxxxxxxxxxxxxxxx"
            }
        });
        _logger = Substitute.For<ILogger<PayStackPaymentProvider>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    private PayStackPaymentProvider CreateSut(HttpClient httpClient)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("PayStack").Returns(httpClient);
        return new PayStackPaymentProvider(factory, _options, _logger);
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, object responseBody)
    {
        var handler = new MockHttpMessageHandler(statusCode, JsonSerializer.Serialize(responseBody));
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.paystack.co")
        };
    }

    [Fact]
    public async Task Should_CreateCustomer_Successfully()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = true,
            message = "Customer created",
            data = new
            {
                customer_code = "CUS_test123",
                email = "user@example.com",
                id = 12345
            }
        });

        var sut = CreateSut(httpClient);
        var request = new CreateCustomerRequest("user@example.com", "John Doe", "Acme Corp");

        var result = await sut.CreateCustomerAsync(request);

        result.Should().NotBeNull();
        result.ExternalCustomerId.Should().Be("CUS_test123");
        result.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task Should_ThrowWhenApiReturns400()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.BadRequest, new
        {
            status = false,
            message = "Invalid email"
        });

        var sut = CreateSut(httpClient);
        var request = new CreateCustomerRequest("invalid", "John", null);

        var act = () => sut.CreateCustomerAsync(request);

        await act.Should().ThrowAsync<PayStackApiException>()
            .WithMessage("*Invalid email*");
    }

    [Fact]
    public async Task Should_InitiatePayment_ReturnAuthorizationUrl()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = true,
            message = "Authorization URL created",
            data = new
            {
                authorization_url = "https://checkout.paystack.com/abc123",
                reference = "ref_test_123",
                access_code = "nms6gnr09qs26gp"
            }
        });

        var sut = CreateSut(httpClient);
        var request = new InitiatePaymentRequest(
            "CUS_test123",
            2999, // kobo
            "NGN",
            "Pro Plan - Monthly",
            "https://app.example.com/billing/callback");

        var result = await sut.InitiatePaymentAsync(request);

        result.Should().NotBeNull();
        result.PaymentUrl.Should().Be("https://checkout.paystack.com/abc123");
        result.ExternalPaymentId.Should().Be("ref_test_123");
        result.Status.Should().Be("pending");
    }

    [Fact]
    public async Task Should_VerifyPayment_ReturnTrue_WhenSuccessful()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = true,
            message = "Verification successful",
            data = new
            {
                status = "success",
                reference = "ref_test_123",
                amount = 2999
            }
        });

        var sut = CreateSut(httpClient);
        var result = await sut.VerifyPaymentAsync("ref_test_123");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_VerifyPayment_ReturnFalse_WhenFailed()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = true,
            message = "Verification successful",
            data = new
            {
                status = "failed",
                reference = "ref_test_123"
            }
        });

        var sut = CreateSut(httpClient);
        var result = await sut.VerifyPaymentAsync("ref_test_123");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_CreateSubscription_ParsePeriodDatesFromResponse()
    {
        var createdAt = new DateTime(2026, 4, 14, 10, 0, 0, DateTimeKind.Utc);
        var nextPaymentDate = createdAt.AddDays(30);

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = true,
            message = "Subscription created",
            data = new
            {
                subscription_code = "SUB_abc123",
                status = "active",
                email_token = "tok_xyz",
                next_payment_date = nextPaymentDate.ToString("o"),
                createdAt = createdAt.ToString("o")
            }
        });

        var sut = CreateSut(httpClient);
        var request = new CreateSubscriptionRequest("CUS_test123", "PLN_test");

        var result = await sut.CreateSubscriptionAsync(request);

        result.Should().NotBeNull();
        result.ExternalSubscriptionId.Should().Be("SUB_abc123");
        result.Status.Should().Be("active");
        result.CurrentPeriodStart.Should().Be(createdAt);
        result.CurrentPeriodEnd.Should().Be(nextPaymentDate);
    }

    [Fact]
    public async Task Should_CreateSubscription_FallbackToUtcNowStart_WhenCreatedAtMissing()
    {
        var nextPaymentDate = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc);

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = true,
            message = "Subscription created",
            data = new
            {
                subscription_code = "SUB_abc123",
                status = "active",
                next_payment_date = nextPaymentDate.ToString("o")
                // createdAt intentionally omitted
            }
        });

        var before = DateTime.UtcNow;
        var sut = CreateSut(httpClient);

        var result = await sut.CreateSubscriptionAsync(
            new CreateSubscriptionRequest("CUS_test123", "PLN_test"));

        var after = DateTime.UtcNow;

        result.CurrentPeriodEnd.Should().Be(nextPaymentDate);
        result.CurrentPeriodStart.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Should_CreateSubscription_FallbackAndWarn_WhenNextPaymentDateMissing()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = true,
            message = "Subscription created",
            data = new
            {
                subscription_code = "SUB_missing_date",
                status = "active"
                // next_payment_date omitted
            }
        });

        var before = DateTime.UtcNow;
        var sut = CreateSut(httpClient);

        var result = await sut.CreateSubscriptionAsync(
            new CreateSubscriptionRequest("CUS_test123", "PLN_test"));

        var after = DateTime.UtcNow;

        // Falls back to UtcNow + 30 days
        result.CurrentPeriodStart.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        result.CurrentPeriodEnd.Should().BeCloseTo(before.AddDays(30), TimeSpan.FromSeconds(5));

        // Warning logged via LoggerMessage source generator
        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Any(c => (LogLevel)c.GetArguments()[0]! == LogLevel.Warning)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Should_CreateSubscription_FallbackAndWarn_WhenNextPaymentDateMalformed()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new
        {
            status = true,
            message = "Subscription created",
            data = new
            {
                subscription_code = "SUB_malformed",
                status = "active",
                next_payment_date = "not-a-real-date"
            }
        });

        var before = DateTime.UtcNow;
        var sut = CreateSut(httpClient);

        var result = await sut.CreateSubscriptionAsync(
            new CreateSubscriptionRequest("CUS_test123", "PLN_test"));

        result.CurrentPeriodEnd.Should().BeCloseTo(before.AddDays(30), TimeSpan.FromSeconds(5));

        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Any(c => (LogLevel)c.GetArguments()[0]! == LogLevel.Warning)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Should_ParseWebhook_ValidSignature()
    {
        var payload = JsonSerializer.Serialize(new
        {
            @event = "charge.success",
            data = new
            {
                reference = "ref_test_123",
                customer = new { customer_code = "CUS_test123" },
                status = "success"
            }
        });

        var signature = ComputeHmacSha512(payload, TestSecretKey);

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new { }); // Not used for webhooks
        var sut = CreateSut(httpClient);

        var result = await sut.ParseWebhookAsync(payload, signature);

        result.Should().NotBeNull();
        result!.EventType.Should().Be("charge.success");
        result.ExternalId.Should().Be("ref_test_123");
        result.ExternalCustomerId.Should().Be("CUS_test123");
    }

    [Fact]
    public async Task Should_RejectWebhook_InvalidSignature()
    {
        var payload = JsonSerializer.Serialize(new
        {
            @event = "charge.success",
            data = new { reference = "ref_test_123" }
        });

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new { });
        var sut = CreateSut(httpClient);

        var result = await sut.ParseWebhookAsync(payload, "invalid_signature");

        result.Should().BeNull();
    }

    private static string ComputeHmacSha512(string payload, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexStringLower(hash);
    }
}

/// <summary>
/// Simple mock HTTP message handler for testing.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public MockHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

/// <summary>
/// Mock handler that returns different responses for sequential requests.
/// Used for multi-step flows like subscription creation.
/// </summary>
internal sealed class SequentialMockHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(HttpStatusCode StatusCode, string ResponseBody)> _responses;
    private int _callIndex;

    public SequentialMockHttpMessageHandler(List<(HttpStatusCode StatusCode, string ResponseBody)> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (statusCode, responseBody) = _callIndex < _responses.Count
            ? _responses[_callIndex]
            : _responses[^1];

        _callIndex++;

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
