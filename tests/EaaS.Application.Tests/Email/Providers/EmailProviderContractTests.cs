using EaaS.Application.Email.Providers;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace EaaS.Application.Tests.Email.Providers;

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
        new()
        {
            From = from ?? "sender@example.com",
            To = to ?? new[] { "recipient@example.com" },
            Subject = "Contract Test",
            TextBody = "Hello",
            HtmlBody = "<p>Hello</p>",
        };

    // -----------------------------------------------------------------
    // 1. Identity + capabilities
    // -----------------------------------------------------------------

    [Fact]
    public void ProviderName_IsNonEmptyConstant()
    {
        var a = CreateProvider().ProviderName;
        var b = CreateProvider().ProviderName;

        a.Should().NotBeNullOrWhiteSpace("every provider must expose a stable identifier");
        b.Should().Be(a, "provider name must be constant across instances");
    }

    [Fact]
    public void Capabilities_MaxRecipients_IsPositive()
    {
        var caps = CreateProvider().Capabilities;

        caps.Should().NotBeNull();
        caps.MaxRecipients.Should().BePositive("a provider that accepts zero recipients is not useful");
    }

    // -----------------------------------------------------------------
    // 2. Happy path
    // -----------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithValidRequest_ReturnsSuccessWithProviderMessageId()
    {
        var provider = CreateProvider();
        ArrangeScenario(provider, ContractScenario.Success);

        var result = await provider.SendAsync(ValidRequest());

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().NotBeNullOrWhiteSpace();
        result.ErrorCode.Should().BeNull();
    }

    // -----------------------------------------------------------------
    // 3. Validation (pre-network)
    // -----------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithEmptyRecipientList_ThrowsValidationException()
    {
        var provider = CreateProvider();
        var request = ValidRequest(to: Array.Empty<string>());

        var act = () => provider.SendAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SendAsync_WithInvalidFromAddress_ReturnsFailureNotRetryable()
    {
        var provider = CreateProvider();
        ArrangeScenario(provider, ContractScenario.InvalidFromAddress);
        var request = ValidRequest(from: "not-a-real-address");

        var result = await provider.SendAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.IsRetryable.Should().BeFalse("malformed sender is a permanent client error");
        result.ErrorCode.Should().NotBeNullOrWhiteSpace();
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

        var result = await provider.SendAsync(ValidRequest());

        result.IsSuccess.Should().BeFalse();
        result.IsRetryable.Should().Be(expectedRetryable);
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
        Skip.IfNot(provider.Capabilities.SupportsAttachments, "provider does not support attachments");
        ArrangeScenario(provider, ContractScenario.Success);

        var request = ValidRequest() with
        {
            Attachments = new[]
            {
                new EmailAttachment("invoice.pdf", "application/pdf", new byte[] { 1, 2, 3 }),
            },
        };

        var result = await provider.SendAsync(request);

        result.IsSuccess.Should().BeTrue();
    }

    [SkippableFact]
    public async Task SendAsync_WithCustomVariables_PassesThroughAndReturnsSuccess()
    {
        var provider = CreateProvider();
        Skip.IfNot(provider.Capabilities.SupportsCustomVariables, "provider does not support custom variables");
        ArrangeScenario(provider, ContractScenario.Success);

        var request = ValidRequest() with
        {
            CustomVariables = new Dictionary<string, string>
            {
                ["tenant_id"] = "tenant-42",
                ["campaign"] = "welcome",
            },
        };

        var result = await provider.SendAsync(request);

        result.IsSuccess.Should().BeTrue();
    }

    [SkippableFact]
    public async Task SendAsync_WithTags_PassesThroughAndReturnsSuccess()
    {
        var provider = CreateProvider();
        Skip.IfNot(provider.Capabilities.SupportsTags, "provider does not support tags");
        ArrangeScenario(provider, ContractScenario.Success);

        var request = ValidRequest() with { Tags = new[] { "transactional", "welcome" } };

        var result = await provider.SendAsync(request);

        result.IsSuccess.Should().BeTrue();
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

        var result = await provider.SendAsync(request);

        result.IsSuccess.Should().BeTrue();
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
