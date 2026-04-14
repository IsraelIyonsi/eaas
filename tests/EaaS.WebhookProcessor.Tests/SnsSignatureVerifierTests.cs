using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EaaS.WebhookProcessor.Configuration;
using EaaS.WebhookProcessor.Handlers;
using EaaS.WebhookProcessor.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace EaaS.WebhookProcessor.Tests;

public class SnsSignatureVerifierTests : IDisposable
{
    private const string ValidCertUrl = "https://sns.us-east-1.amazonaws.com/SimpleNotificationService-abc123.pem";

    // Anchor "now" to the timestamp used in test SNS messages so skew validation passes by default.
    private static readonly DateTimeOffset TestNow = new(2026, 4, 14, 0, 0, 0, TimeSpan.Zero);

    private readonly RSA _rsa;
    private readonly X509Certificate2 _cert;
    private readonly FakeTimeProvider _timeProvider;

    public SnsSignatureVerifierTests()
    {
        _rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=sns.amazonaws.com",
            _rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        _cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        _timeProvider = new FakeTimeProvider(TestNow);
        SnsSignatureVerifier.ClearCertCache();
        SnsSignatureVerifier.SetCachedCertificate(ValidCertUrl, _cert, TestNow.AddHours(1));
    }

    public void Dispose()
    {
        SnsSignatureVerifier.ClearCertCache();
        _cert.Dispose();
        _rsa.Dispose();
        GC.SuppressFinalize(this);
    }

    private SnsSignatureVerifier BuildVerifier(HttpMessageHandler? handler = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler ?? new ThrowingHandler()));
        return new SnsSignatureVerifier(factory, NullLogger<SnsSignatureVerifier>.Instance, _timeProvider);
    }

    private string Sign(string canonical)
    {
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var sig = _rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(sig);
    }

    private const string IsoFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    private SnsMessage BuildNotification(string messageBody = "hello", string? timestamp = null)
    {
        var msg = new SnsMessage
        {
            Type = "Notification",
            MessageId = "msg-1",
            TopicArn = "arn:aws:sns:us-east-1:123456789012:ses-events",
            Message = messageBody,
            Timestamp = timestamp ?? TestNow.ToString(IsoFormat, CultureInfo.InvariantCulture),
            SignatureVersion = "1",
            SigningCertUrl = ValidCertUrl
        };
        var canonical = SnsSignatureVerifier.BuildCanonicalString(msg)!;
        msg.Signature = Sign(canonical);
        return msg;
    }

    [Fact]
    public async Task Verify_ReturnsTrue_ForValidSignature()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification();

        var result = await verifier.VerifyAsync(msg, "req-1", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenBodyTampered()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification("original");
        msg.Message = "tampered";

        var result = await verifier.VerifyAsync(msg, "req-2", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenSigningCertUrlIsNotAwsDomain()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification();
        msg.SigningCertUrl = "https://evil.example.com/cert.pem";

        var result = await verifier.VerifyAsync(msg, "req-3", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenSigningCertUrlIsAwsLookalike()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification();
        msg.SigningCertUrl = "https://sns.us-east-1.amazonaws.com.evil.com/cert.pem";

        var result = await verifier.VerifyAsync(msg, "req-4", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://sns.evil.amazonaws.com/cert.pem")]             // attacker subdomain
    [InlineData("https://sns.a.b.us-east-1.amazonaws.com/cert.pem")]    // extra labels
    [InlineData("http://sns.us-east-1.amazonaws.com/cert.pem")]         // http scheme
    public async Task Verify_ReturnsFalse_ForDisallowedHostShapes(string url)
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification();
        msg.SigningCertUrl = url;

        var result = await verifier.VerifyAsync(msg, "req-host", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://sns.us-east-1.amazonaws.com/SimpleNotificationService-abc.pem")]
    [InlineData("https://sns.eu-west-2.amazonaws.com/SimpleNotificationService-XYZ123.pem")]
    [InlineData("https://sns.ap-southeast-1.amazonaws.com/SimpleNotificationService-9f8e.pem")]
    public void IsValidSigningCertUrl_AcceptsExactRegionalSnsHosts(string url)
    {
        SnsValidation.IsValidSigningCertUrl(url).Should().BeTrue();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenRequiredFieldMissing()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification();
        msg.Timestamp = null;

        var result = await verifier.VerifyAsync(msg, "req-5", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenSignatureVersionUnsupported()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification();
        msg.SignatureVersion = "99";

        var result = await verifier.VerifyAsync(msg, "req-6", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenSignatureIsNotValidBase64()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification();
        msg.Signature = "%%%not-base64%%%";

        var result = await verifier.VerifyAsync(msg, "req-7", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ReturnsTrue_ForValidSubscriptionConfirmation()
    {
        var verifier = BuildVerifier();
        var msg = new SnsMessage
        {
            Type = "SubscriptionConfirmation",
            MessageId = "msg-sub",
            Token = "token-xyz",
            TopicArn = "arn:aws:sns:us-east-1:123456789012:ses-events",
            Message = "confirm",
            SubscribeUrl = "https://sns.us-east-1.amazonaws.com/?Action=ConfirmSubscription",
            Timestamp = TestNow.ToString(IsoFormat, CultureInfo.InvariantCulture),
            SignatureVersion = "1",
            SigningCertUrl = ValidCertUrl
        };
        msg.Signature = Sign(SnsSignatureVerifier.BuildCanonicalString(msg)!);

        var result = await verifier.VerifyAsync(msg, "req-8", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public void BuildCanonicalString_IncludesSubjectOnlyWhenPresent()
    {
        var msgNoSubject = new SnsMessage
        {
            Type = "Notification",
            MessageId = "m1",
            TopicArn = "arn",
            Message = "body",
            Timestamp = "t"
        };
        var canonical = SnsSignatureVerifier.BuildCanonicalString(msgNoSubject)!;
        canonical.Should().NotContain("Subject\n");

        var msgWithSubject = new SnsMessage
        {
            Type = "Notification",
            MessageId = "m1",
            Subject = "hello",
            TopicArn = "arn",
            Message = "body",
            Timestamp = "t"
        };
        var canonical2 = SnsSignatureVerifier.BuildCanonicalString(msgWithSubject)!;
        canonical2.Should().Contain("Subject\nhello\n");
    }

    // ---------- Timestamp skew ----------

    [Fact]
    public async Task Verify_ReturnsFalse_WhenTimestampTwoHoursInPast()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification(timestamp: TestNow.AddHours(-2).ToString(IsoFormat, CultureInfo.InvariantCulture));

        var result = await verifier.VerifyAsync(msg, "req-skew-past", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenTimestampTwoHoursInFuture()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification(timestamp: TestNow.AddHours(2).ToString(IsoFormat, CultureInfo.InvariantCulture));

        var result = await verifier.VerifyAsync(msg, "req-skew-future", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ReturnsTrue_WhenTimestampWithinSkewWindow()
    {
        // Default MaxClockSkew is 15 minutes — 10m is comfortably inside that window.
        var verifier = BuildVerifier();
        var msg = BuildNotification(timestamp: TestNow.AddMinutes(-10).ToString(IsoFormat, CultureInfo.InvariantCulture));

        var result = await verifier.VerifyAsync(msg, "req-skew-ok", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenTimestampOutsideTightenedSkewWindow()
    {
        // Sanity check on the tightened default (15m): 20 minutes in the past must now reject.
        var verifier = BuildVerifier();
        var msg = BuildNotification(timestamp: TestNow.AddMinutes(-20).ToString(IsoFormat, CultureInfo.InvariantCulture));

        var result = await verifier.VerifyAsync(msg, "req-skew-tight", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenTimestampUnparseable()
    {
        var verifier = BuildVerifier();
        var msg = BuildNotification();
        msg.Timestamp = "not-a-date";
        // Re-sign against the mutated canonical string so we isolate the timestamp-parse failure.
        msg.Signature = Sign(SnsSignatureVerifier.BuildCanonicalString(msg)!);

        var result = await verifier.VerifyAsync(msg, "req-skew-bad", CancellationToken.None);

        result.Should().BeFalse();
    }

    // ---------- SignatureVersion 2 (ECDSA) ----------

    [Fact]
    public async Task Verify_ReturnsTrue_ForV2EcdsaSignature()
    {
        // v2 certs issued by AWS are ECDSA; verify we accept them.
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=sns.amazonaws.com", ecdsa, HashAlgorithmName.SHA256);
        using var ecCert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        const string v2Url = "https://sns.us-east-1.amazonaws.com/SimpleNotificationService-v2.pem";
        SnsSignatureVerifier.SetCachedCertificate(v2Url, ecCert, TestNow.AddHours(1));

        var msg = new SnsMessage
        {
            Type = "Notification",
            MessageId = "msg-v2",
            TopicArn = "arn:aws:sns:us-east-1:123456789012:ses-events",
            Message = "hello-v2",
            Timestamp = TestNow.ToString(IsoFormat, CultureInfo.InvariantCulture),
            SignatureVersion = "2",
            SigningCertUrl = v2Url
        };
        var canonical = SnsSignatureVerifier.BuildCanonicalString(msg)!;
        var sig = ecdsa.SignData(Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256);
        msg.Signature = Convert.ToBase64String(sig);

        var verifier = BuildVerifier();
        var result = await verifier.VerifyAsync(msg, "req-v2", CancellationToken.None);

        result.Should().BeTrue();
    }

    // ---------- Cert-fetch failure ----------

    [Fact]
    public async Task Verify_ReturnsFalse_WhenCertFetchThrows()
    {
        // Bypass the seeded cache by using a URL that isn't cached; the factory-returned handler throws.
        const string freshUrl = "https://sns.us-east-1.amazonaws.com/SimpleNotificationService-uncached.pem";
        SnsSignatureVerifier.ClearCertCache();

        var verifier = BuildVerifier(new ThrowingHandler());
        var msg = BuildNotification();
        msg.SigningCertUrl = freshUrl;
        // Signature will never be checked — cert fetch fails first — but set a plausible base64 value.
        msg.Signature = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var result = await verifier.VerifyAsync(msg, "req-fetch-fail", CancellationToken.None);

        result.Should().BeFalse();
    }

    // ---------- Bounded cert cache ----------

    [Fact]
    public void CertCache_EvictsOldestWhenCapacityExceeded()
    {
        SnsSignatureVerifier.ClearCertCache();

        // Insert exactly CacheCapacity + 1 entries; oldest must be evicted.
        var firstUrl = $"https://sns.us-east-1.amazonaws.com/cert-0.pem";
        SnsSignatureVerifier.SetCachedCertificate(firstUrl, _cert, TestNow.AddHours(1));
        for (var i = 1; i < SnsSignatureVerifier.CacheCapacity; i++)
        {
            SnsSignatureVerifier.SetCachedCertificate(
                $"https://sns.us-east-1.amazonaws.com/cert-{i}.pem",
                _cert,
                TestNow.AddHours(1));
        }

        SnsSignatureVerifier.CertCacheCount.Should().Be(SnsSignatureVerifier.CacheCapacity);
        SnsSignatureVerifier.CertCacheContains(firstUrl).Should().BeTrue();

        // 33rd insert — capacity is 32 — must evict the least-recently-used entry (the first one).
        SnsSignatureVerifier.SetCachedCertificate(
            $"https://sns.us-east-1.amazonaws.com/cert-overflow.pem",
            _cert,
            TestNow.AddHours(1));

        SnsSignatureVerifier.CertCacheCount.Should().Be(SnsSignatureVerifier.CacheCapacity);
        SnsSignatureVerifier.CertCacheContains(firstUrl).Should().BeFalse();
    }

    // ---------- Cert-fetch coalescing + negative cache ----------

    [Fact]
    public async Task Verify_CoalescesConcurrentCertFetches_ForSameUrl()
    {
        // Multiple concurrent VerifyAsync calls missing the positive cache for the same URL must share
        // a single outbound HTTP fetch.
        SnsSignatureVerifier.ClearCertCache();

        const string url = "https://sns.us-east-1.amazonaws.com/SimpleNotificationService-coalesce.pem";
        var pem = ExportCertToPem(_cert);
        var handler = new CountingPemHandler(pem, gateMs: 50);
        var verifier = BuildVerifier(handler);

        // Build 10 messages that target the uncached URL with a valid signature.
        var tasks = Enumerable.Range(0, 10).Select(i =>
        {
            var msg = BuildNotification();
            msg.SigningCertUrl = url;
            msg.MessageId = $"msg-coalesce-{i}";
            // Re-sign with new MessageId so canonical hash differs, but all point to same cert URL.
            msg.Signature = Sign(SnsSignatureVerifier.BuildCanonicalString(msg)!);
            return verifier.VerifyAsync(msg, $"req-coalesce-{i}", CancellationToken.None);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.Should().BeTrue());
        handler.CallCount.Should().Be(1, "concurrent cache misses for the same URL should share one outbound fetch");
    }

    [Fact]
    public async Task Verify_NegativeCache_ReusesFailure_WithoutExtraOutboundCalls()
    {
        SnsSignatureVerifier.ClearCertCache();

        const string url = "https://sns.us-east-1.amazonaws.com/SimpleNotificationService-negcache.pem";
        var handler = new CountingThrowingHandler();
        var verifier = BuildVerifier(handler);

        var msg1 = BuildNotification();
        msg1.SigningCertUrl = url;
        msg1.Signature = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        // First call: fetch throws, negative cache gets populated.
        (await verifier.VerifyAsync(msg1, "req-neg-1", CancellationToken.None)).Should().BeFalse();
        handler.CallCount.Should().Be(1);

        // Second call within negative-cache TTL (default 60s) must NOT hit the network.
        var msg2 = BuildNotification();
        msg2.SigningCertUrl = url;
        msg2.Signature = Convert.ToBase64String(new byte[] { 4, 5, 6 });
        (await verifier.VerifyAsync(msg2, "req-neg-2", CancellationToken.None)).Should().BeFalse();
        handler.CallCount.Should().Be(1, "negative cache should short-circuit subsequent fetches");

        SnsSignatureVerifier.NegativeCacheContains(url).Should().BeTrue();
    }

    private static string ExportCertToPem(X509Certificate2 cert)
    {
        var b64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN CERTIFICATE-----");
        for (var i = 0; i < b64.Length; i += 64)
        {
            sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
        }
        sb.AppendLine("-----END CERTIFICATE-----");
        return sb.ToString();
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Cert HTTP fetch failed (test-induced).");
    }

    private sealed class CountingThrowingHandler : HttpMessageHandler
    {
        public int CallCount;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            throw new HttpRequestException("negative-cache test throw");
        }
    }

    private sealed class CountingPemHandler : HttpMessageHandler
    {
        private readonly string _pem;
        private readonly int _gateMs;
        public int CallCount;

        public CountingPemHandler(string pem, int gateMs)
        {
            _pem = pem;
            _gateMs = gateMs;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            // Artificial delay lets the other 9 callers race into the coalescing gate before this returns.
            await Task.Delay(_gateMs, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_pem)
            };
        }
    }
}
