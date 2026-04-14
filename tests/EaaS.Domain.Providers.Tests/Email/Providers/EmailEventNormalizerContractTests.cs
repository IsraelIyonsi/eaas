using EaaS.Domain.Providers;
using FluentAssertions;
using Xunit;

namespace EaaS.Domain.Providers.Tests.Email.Providers;

/// <summary>
/// Provider-agnostic event-normalization contract. Concrete subclasses supply
/// real provider payload samples for each event kind.
/// </summary>
public abstract class EmailEventNormalizerContractTests<TNormalizer>
    where TNormalizer : IEmailEventNormalizer
{
    protected abstract TNormalizer CreateNormalizer();

    /// <summary>Return a provider-specific payload representing the requested event kind.</summary>
    protected abstract ReadOnlyMemory<byte> BuildPayload(NormalizerScenario scenario);

    protected virtual IReadOnlyDictionary<string, string> BuildHeaders(NormalizerScenario scenario) =>
        new Dictionary<string, string>();

    public static TheoryData<NormalizerScenario, EmailEventType> EventMappings =>
        new()
        {
            { NormalizerScenario.Delivered, EmailEventType.Delivered },
            { NormalizerScenario.HardBounce, EmailEventType.PermFailed },
            { NormalizerScenario.SoftBounce, EmailEventType.TempFailed },
            { NormalizerScenario.Complaint, EmailEventType.Complained },
            { NormalizerScenario.Click, EmailEventType.Clicked },
            { NormalizerScenario.Open, EmailEventType.Opened },
            { NormalizerScenario.Unsubscribe, EmailEventType.Unsubscribed },
        };

    [Theory]
    [MemberData(nameof(EventMappings))]
    public async Task NormalizeAsync_MapsProviderEventToExpectedType(NormalizerScenario scenario, EmailEventType expected)
    {
        var normalizer = CreateNormalizer();
        var payload = BuildPayload(scenario);
        var headers = BuildHeaders(scenario);

        var events = await normalizer.NormalizeAsync(payload, headers);

        events.Should().ContainSingle()
            .Which.Type.Should().Be(expected);
    }

    [Fact]
    public async Task NormalizeAsync_UnknownEventType_DoesNotThrowAndYieldsEmpty()
    {
        var normalizer = CreateNormalizer();
        var payload = BuildPayload(NormalizerScenario.Unknown);

        var act = async () => await normalizer.NormalizeAsync(payload, BuildHeaders(NormalizerScenario.Unknown));

        var result = await act.Should().NotThrowAsync();
        result.Subject.Should().BeEmpty("unknown event types must be silently dropped for forward-compatibility");
    }

    [Fact]
    public async Task NormalizeAsync_EveryEventCarriesNormalizerProviderKey()
    {
        var normalizer = CreateNormalizer();
        var payload = BuildPayload(NormalizerScenario.Delivered);

        var events = await normalizer.NormalizeAsync(payload, BuildHeaders(NormalizerScenario.Delivered));

        events.Should().NotBeEmpty();
        foreach (var evt in events)
        {
            evt.ProviderKey.Should().Be(
                normalizer.ProviderKey,
                "downstream routing depends on the originating provider key being stamped on every event");
        }
    }

    public enum NormalizerScenario
    {
        Delivered,
        HardBounce,
        SoftBounce,
        Complaint,
        Click,
        Open,
        Unsubscribe,
        Unknown,
    }
}
