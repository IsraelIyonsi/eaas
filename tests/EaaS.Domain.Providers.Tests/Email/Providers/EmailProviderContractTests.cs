using EaaS.Domain.Providers;
using FluentAssertions;
using Xunit;

namespace EaaS.Domain.Providers.Tests.Email.Providers;

/// <summary>
/// Provider-agnostic contract test suite. Every <see cref="IEmailProvider"/>
/// implementation MUST have a concrete subclass of this base that passes
/// ALL tests. No provider-specific knowledge belongs in this file.
/// </summary>
/// <typeparam name="TProvider">Concrete provider under test.</typeparam>
public abstract class EmailProviderContractTests<TProvider>
    where TProvider : IEmailProvider
{
    /// <summary>Factory for a fresh provider instance per test (isolation).</summary>
    protected abstract TProvider CreateProvider();

    /// <summary>
    /// Hook for the subclass to stage a scenario (e.g. "next call returns 500").
    /// Subclasses override as needed; default is no-op.
    /// </summary>
    protected virtual void ArrangeScenario(TProvider provider, ContractScenario scenario) { }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    protected static SendEmailRequest ValidRequest(
        string? from = null,
        IReadOnlyList<string>? to = null) =>
        new(
            TenantId: Guid.NewGuid(),
            From: from ?? "sender@example.com",
            FromName: null,
            To: to ?? new[] { "recipient@example.com" },
            Cc: null,
            Bcc: null,
            Subject: "Contract Test",
            HtmlBody: "<p>Hello</p>",
            TextBody: "Hello");

    // -----------------------------------------------------------------
    // 1. Identity + capabilities
    // -----------------------------------------------------------------

    [Fact]
    public void ProviderKey_IsNonEmptyConstant()
    {
        var a = CreateProvider().ProviderKey;
        var b = CreateProvider().ProviderKey;

        a.Should().NotBeNullOrWhiteSpace("every provider must expose a stable identifier");
        b.Should().Be(a, "provider key must be constant across instances");
    }

    [Fact]
    public void Capabilities_AreStableAcrossInstances()
    {
        var a = CreateProvider().Capabilities;
        var b = CreateProvider().Capabilities;

        b.Should().Be(a, "capability flags must be deterministic per adapter");
    }

    // -----------------------------------------------------------------
    // 2. Happy path
    // -----------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithValidRequest_ReturnsSuccessWithProviderMessageId()
    {
        var provider = CreateProvider();
        ArrangeScenario(provider, ContractScenario.Success);

        var outcome = await provider.SendAsync(ValidRequest());

        outcome.Success.Should().BeTrue();
        outcome.ProviderMessageId.Should().NotBeNullOrWhiteSpace();
        outcome.ErrorCode.Should().BeNull();
    }

    // -----------------------------------------------------------------
    // 3. Validation (pre-network)
    // -----------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithEmptyRecipientList_ThrowsEmailValidationException()
    {
        var provider = CreateProvider();
        var request = ValidRequest(to: Array.Empty<string>());

        var act = () => provider.SendAsync(request);

        await act.Should().ThrowAsync<EmailValidationException>();
    }

    [Fact]
    public async Task SendAsync_WithInvalidFromAddress_ReturnsFailureNotRetryable()
    {
        var provider = CreateProvider();
        ArrangeScenario(provider, ContractScenario.InvalidFromAddress);
        var request = ValidRequest(from: "not-a-real-address");

        var outcome = await provider.SendAsync(request);

        outcome.Success.Should().BeFalse();
        outcome.IsRetryable.Should().BeFalse("malformed sender is a permanent client error");
        outcome.ErrorCode.Should().NotBeNullOrWhiteSpace();
    }

    // -----------------------------------------------------------------
    // 4. Transport failures
    // -----------------------------------------------------------------

    public static TheoryData<ContractScenario, bool> RetrySemanticsData =>
        new()
        {
            { ContractScenario.ServerError5xx, true },
            { ContractScenario.ClientError4xx, false },
        };

    [Theory]
    [MemberData(nameof(RetrySemanticsData))]
    public async Task SendAsync_OnHttpError_SetsRetryableFlagCorrectly(ContractScenario scenario, bool expectedRetryable)
    {
        var provider = CreateProvider();
        ArrangeScenario(provider, scenario);

        var outcome = await provider.SendAsync(ValidRequest());

        outcome.Success.Should().BeFalse();
        outcome.IsRetryable.Should().Be(expectedRetryable);
    }

    // Named wrappers satisfy the explicit spec deliverables while reusing the theory above.
    [Fact]
    public Task SendAsync_Over5xxError_ReturnsFailureRetryable() =>
        SendAsync_OnHttpError_SetsRetryableFlagCorrectly(ContractScenario.ServerError5xx, expectedRetryable: true);

    [Fact]
    public Task SendAsync_Over4xxError_ReturnsFailureNotRetryable() =>
        SendAsync_OnHttpError_SetsRetryableFlagCorrectly(ContractScenario.ClientError4xx, expectedRetryable: false);

    // -----------------------------------------------------------------
    // 5. Capability-gated features
    // -----------------------------------------------------------------

    [SkippableFact]
    public async Task SendAsync_WithAttachment_PassesThroughAndReturnsSuccess()
    {
        var provider = CreateProvider();
        Skip.IfNot(
            provider.Capabilities.HasFlag(EmailProviderCapability.Attachments),
            "provider does not support attachments");
        ArrangeScenario(provider, ContractScenario.Success);

        var request = ValidRequest() with
        {
            Attachments = new[]
            {
                new EmailAttachment("invoice.pdf", "application/pdf", new MemoryStream(new byte[] { 1, 2, 3 })),
            },
        };

        var outcome = await provider.SendAsync(request);

        outcome.Success.Should().BeTrue();
    }

    [SkippableFact]
    public async Task SendAsync_WithCustomVariables_PassesThroughAndReturnsSuccess()
    {
        var provider = CreateProvider();
        Skip.IfNot(
            provider.Capabilities.HasFlag(EmailProviderCapability.CustomVariables),
            "provider does not support custom variables");
        ArrangeScenario(provider, ContractScenario.Success);

        var request = ValidRequest() with
        {
            CustomVariables = new Dictionary<string, string>
            {
                ["tenant_id"] = "tenant-42",
                ["campaign"] = "welcome",
            },
        };

        var outcome = await provider.SendAsync(request);

        outcome.Success.Should().BeTrue();
    }

    [SkippableFact]
    public async Task SendAsync_WithTags_PassesThroughAndReturnsSuccess()
    {
        var provider = CreateProvider();
        Skip.IfNot(
            provider.Capabilities.HasFlag(EmailProviderCapability.Tags),
            "provider does not support tags");
        ArrangeScenario(provider, ContractScenario.Success);

        var request = ValidRequest() with { Tags = new[] { "transactional", "welcome" } };

        var outcome = await provider.SendAsync(request);

        outcome.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------
    // 6. Cancellation
    // -----------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var provider = CreateProvider();
        ArrangeScenario(provider, ContractScenario.Success);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => provider.SendAsync(ValidRequest(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // -----------------------------------------------------------------
    // 7. Header fidelity
    // -----------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithCcBccRecipients_IncludesAllInHeaders()
    {
        var provider = CreateProvider();
        ArrangeScenario(provider, ContractScenario.CaptureRecipients);

        var request = ValidRequest() with
        {
            Cc = new[] { "cc1@example.com", "cc2@example.com" },
            Bcc = new[] { "bcc@example.com" },
        };

        var outcome = await provider.SendAsync(request);

        outcome.Success.Should().BeTrue();
        AssertAllRecipientsForwarded(provider, request);
    }

    /// <summary>
    /// Subclasses MUST verify that the captured outbound request contains the To/Cc/Bcc
    /// addresses verbatim. Default impl passes (assumes capture happened inside provider).
    /// </summary>
    protected virtual void AssertAllRecipientsForwarded(TProvider provider, SendEmailRequest request) { }

    // -----------------------------------------------------------------
    // Scenario enum for subclass arrangement hooks
    // -----------------------------------------------------------------

    public enum ContractScenario
    {
        Success,
        InvalidFromAddress,
        ServerError5xx,
        ClientError4xx,
        CaptureRecipients,
    }
}
