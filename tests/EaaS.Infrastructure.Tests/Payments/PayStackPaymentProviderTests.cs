using System.Net;
using System.Net.Http.Json;
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
