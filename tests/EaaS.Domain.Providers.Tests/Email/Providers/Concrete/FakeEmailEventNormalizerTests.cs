using System.Text;
using EaaS.Domain.Providers.Tests.Email.Providers.Fakes;

namespace EaaS.Domain.Providers.Tests.Email.Providers.Concrete;

public sealed class FakeEmailEventNormalizerTests
    : EmailEventNormalizerContractTests<FakeEmailEventNormalizer>
{
    protected override FakeEmailEventNormalizer CreateNormalizer() => new();

    protected override ReadOnlyMemory<byte> BuildPayload(NormalizerScenario scenario) => scenario switch
    {
        NormalizerScenario.Delivered => FakeEmailEventNormalizer.BuildPayload("delivered"),
        NormalizerScenario.HardBounce => FakeEmailEventNormalizer.BuildPayload("bounce", "hard"),
        NormalizerScenario.SoftBounce => FakeEmailEventNormalizer.BuildPayload("bounce", "soft"),
        NormalizerScenario.Complaint => FakeEmailEventNormalizer.BuildPayload("complaint"),
        NormalizerScenario.Click => FakeEmailEventNormalizer.BuildPayload("click"),
        NormalizerScenario.Open => FakeEmailEventNormalizer.BuildPayload("open"),
        NormalizerScenario.Unsubscribe => FakeEmailEventNormalizer.BuildPayload("unsubscribe"),
        NormalizerScenario.Unknown => Encoding.UTF8.GetBytes("""{"type":"quantum_teleported"}"""),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
    };
}
