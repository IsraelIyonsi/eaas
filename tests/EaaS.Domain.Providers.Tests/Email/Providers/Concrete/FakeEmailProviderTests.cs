using EaaS.Domain.Providers;
using EaaS.Domain.Providers.Tests.Email.Providers.Fakes;
using FluentAssertions;

namespace EaaS.Domain.Providers.Tests.Email.Providers.Concrete;

/// <summary>
/// Exercises the <see cref="EmailProviderContractTests{TProvider}"/> base against
/// a reference implementation. If any contract test fails here, the fault is in
/// the contract definition itself, not a real provider.
/// </summary>
public sealed class FakeEmailProviderTests : EmailProviderContractTests<FakeEmailProvider>
{
    protected override FakeEmailProvider CreateProvider() => new();

    protected override void ArrangeScenario(FakeEmailProvider provider, ContractScenario scenario)
    {
        provider.NextMode = scenario switch
        {
            ContractScenario.InvalidFromAddress => FakeEmailProvider.Mode.InvalidFromAddress,
            ContractScenario.ServerError5xx => FakeEmailProvider.Mode.ServerError5xx,
            ContractScenario.ClientError4xx => FakeEmailProvider.Mode.ClientError4xx,
            _ => FakeEmailProvider.Mode.Success,
        };
    }

    protected override void AssertAllRecipientsForwarded(FakeEmailProvider provider, SendEmailRequest request)
    {
        provider.SentRequests.Should().ContainSingle();
        var captured = provider.SentRequests[0];
        captured.To.Should().BeEquivalentTo(request.To);
        captured.Cc.Should().BeEquivalentTo(request.Cc);
        captured.Bcc.Should().BeEquivalentTo(request.Bcc);
    }
}
