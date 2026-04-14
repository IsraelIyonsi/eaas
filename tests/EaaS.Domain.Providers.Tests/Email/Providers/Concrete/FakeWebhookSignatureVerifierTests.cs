using System.Globalization;
using System.Text;
using EaaS.Domain.Providers.Tests.Email.Providers.Fakes;

namespace EaaS.Domain.Providers.Tests.Email.Providers.Concrete;

public sealed class FakeWebhookSignatureVerifierTests
    : WebhookSignatureVerifierContractTests<FakeWebhookSignatureVerifier>
{
    protected override FakeWebhookSignatureVerifier CreateVerifier() => new();

    protected override bool SupportsNonces => false;

    protected override string GetTimestampHeader() => FakeWebhookSignatureVerifier.TimestampHeader;

    protected override SignedPayload BuildValidPayload()
    {
        var body = Encoding.UTF8.GetBytes("""{"type":"delivered"}""");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var sig = FakeWebhookSignatureVerifier.Sign(ts, body);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [FakeWebhookSignatureVerifier.SignatureHeader] = sig,
            [FakeWebhookSignatureVerifier.TimestampHeader] = ts,
        };

        return new SignedPayload(headers, body);
    }
}
