using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EaaS.WebhookProcessor.Handlers;
using EaaS.WebhookProcessor.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EaaS.WebhookProcessor.Tests.TestSupport;

/// <summary>
/// Shared helpers for SnsMessageHandler / SnsInboundHandler tests: self-signed cert cached in the
/// verifier, FakeTimeProvider anchored to match SNS timestamps, and canonical-string signer.
/// </summary>
internal sealed class SnsTestFixture : IDisposable
{
    internal const string ValidCertUrl = "https://sns.us-east-1.amazonaws.com/SimpleNotificationService-abc123.pem";
    private const string IsoFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    internal static readonly DateTimeOffset TestNow = new(2026, 4, 14, 0, 0, 0, TimeSpan.Zero);

    private readonly RSA _rsa;
    private readonly X509Certificate2 _cert;
    internal FakeTimeProvider TimeProvider { get; }
    internal SnsSignatureVerifier Verifier { get; }

    public SnsTestFixture()
    {
        _rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=sns.amazonaws.com",
            _rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        _cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        TimeProvider = new FakeTimeProvider(TestNow);
        SnsSignatureVerifier.ClearCertCache();
        SnsSignatureVerifier.SetCachedCertificate(ValidCertUrl, _cert, TestNow.AddHours(1));

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new ThrowingHandler()));
        Verifier = new SnsSignatureVerifier(factory, NullLogger<SnsSignatureVerifier>.Instance, TimeProvider);
    }

    public void Dispose()
    {
        SnsSignatureVerifier.ClearCertCache();
        _cert.Dispose();
        _rsa.Dispose();
    }

    internal SnsMessage BuildSignedNotification(string messageBody = "{\"notificationType\":\"Bounce\"}", string? messageId = null, string signatureVersion = "1")
    {
        var msg = new SnsMessage
        {
            Type = "Notification",
            MessageId = messageId ?? Guid.NewGuid().ToString(),
            TopicArn = "arn:aws:sns:us-east-1:123456789012:ses-events",
            Message = messageBody,
            Timestamp = TestNow.ToString(IsoFormat, CultureInfo.InvariantCulture),
            SignatureVersion = signatureVersion,
            SigningCertUrl = ValidCertUrl
        };
        msg.Signature = Sign(SnsSignatureVerifier.BuildCanonicalString(msg)!, signatureVersion);
        return msg;
    }

    internal SnsMessage BuildSignedSubscriptionConfirmation(string subscribeUrl = "https://sns.us-east-1.amazonaws.com/?Action=ConfirmSubscription&TopicArn=arn&Token=abc", string signatureVersion = "1")
    {
        var msg = new SnsMessage
        {
            Type = "SubscriptionConfirmation",
            MessageId = Guid.NewGuid().ToString(),
            Token = "token-xyz",
            TopicArn = "arn:aws:sns:us-east-1:123456789012:ses-events",
            Message = "confirm",
            SubscribeUrl = subscribeUrl,
            Timestamp = TestNow.ToString(IsoFormat, CultureInfo.InvariantCulture),
            SignatureVersion = signatureVersion,
            SigningCertUrl = ValidCertUrl
        };
        msg.Signature = Sign(SnsSignatureVerifier.BuildCanonicalString(msg)!, signatureVersion);
        return msg;
    }

    private string Sign(string canonical, string signatureVersion)
    {
        // SigVer "1" → SHA1, SigVer "2" → SHA256 (AWS SNS spec).
        var hash = signatureVersion == "2" ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1;
        var sig = _rsa.SignData(Encoding.UTF8.GetBytes(canonical), hash, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(sig);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => throw new HttpRequestException("no network in tests");
    }
}
