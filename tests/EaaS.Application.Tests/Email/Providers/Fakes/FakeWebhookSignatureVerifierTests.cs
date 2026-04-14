using System.Globalization;
using System.Text;
using EaaS.Application.Email.Providers;

namespace EaaS.Application.Tests.Email.Providers.Fakes;

public sealed class FakeWebhookSignatureVerifierTests
    : WebhookSignatureVerifierContractTests<FakeWebhookSignatureVerifier>
{
    protected override FakeWebhookSignatureVerifier CreateVerifier() => new();

    protected override bool SupportsNonces => false;

    protected override SignedPayload BuildValidPayload()
    {
        var body = Encoding.UTF8.GetBytes("""{"type":"delivered"}""");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var sig = FakeWebhookSignatureVerifier.Sign(ts, body);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Signature"] = sig,
            ["X-Timestamp"] = ts,
        };

        return new SignedPayload(headers, body);
    }
}
