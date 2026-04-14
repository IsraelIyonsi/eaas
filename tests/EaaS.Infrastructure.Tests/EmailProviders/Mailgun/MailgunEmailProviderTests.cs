using EaaS.Domain.Providers;
using EaaS.Infrastructure.EmailProviders.Providers.Mailgun;
using FluentAssertions;
using SendNex.Mailgun;
using SendNex.Mailgun.Dtos;
using Xunit;

namespace EaaS.Infrastructure.Tests.EmailProviders.Mailgun;

public sealed class MailgunEmailProviderTests
{
    private static readonly string[] OneRecipient = { "user@example.com" };
    private static readonly string[] NoRecipients = Array.Empty<string>();

    [Fact]
    public async Task SendAsync_SetsTenantIdCustomVariable()
    {
        var fakeClient = new RecordingMailgunClient();
        var provider = new MailgunEmailProvider(
            fakeClient,
            MailgunTestFixtures.Options(defaultDomain: "mg.example.com"),
            MailgunTestFixtures.Logger<MailgunEmailProvider>());

        var tenantId = Guid.NewGuid();
        var request = new SendEmailRequest(
            TenantId: tenantId,
            From: "sender@example.com",
            FromName: "Sender",
            To: OneRecipient,
            Cc: null,
            Bcc: null,
            Subject: "hi",
            HtmlBody: "<p>hi</p>",
            TextBody: null);

        var outcome = await provider.SendAsync(request);

        outcome.Success.Should().BeTrue();
        outcome.ProviderMessageId.Should().Be("<20260205213049.abc@mg.example.com>");
        fakeClient.LastRequest.Should().NotBeNull();
        fakeClient.LastRequest!.CustomVariables.Should().NotBeNull();
        fakeClient.LastRequest.CustomVariables!
            .Should().ContainKey(MailgunConstants.CustomVariables.TenantId)
            .WhoseValue.Should().Be(tenantId.ToString("D"));
    }

    [Fact]
    public async Task SendAsync_MergesTenantIdWithCallerCustomVariables()
    {
        var fakeClient = new RecordingMailgunClient();
        var provider = new MailgunEmailProvider(
            fakeClient,
            MailgunTestFixtures.Options(defaultDomain: "mg.example.com"),
            MailgunTestFixtures.Logger<MailgunEmailProvider>());

        var tenantId = Guid.NewGuid();
        var request = new SendEmailRequest(
            TenantId: tenantId,
            From: "sender@example.com",
            FromName: null,
            To: OneRecipient,
            Cc: null, Bcc: null,
            Subject: "hi",
            HtmlBody: null,
            TextBody: "hi",
            CustomVariables: new Dictionary<string, string> { ["campaign_id"] = "camp-1" });

        await provider.SendAsync(request);

        fakeClient.LastRequest!.CustomVariables!
            .Should().ContainKey("campaign_id").WhoseValue.Should().Be("camp-1");
        fakeClient.LastRequest.CustomVariables!
            .Should().ContainKey(MailgunConstants.CustomVariables.TenantId);
    }

    [Fact]
    public async Task SendAsync_UsesRequestSendingDomainOverOptionsDefault()
    {
        var fakeClient = new RecordingMailgunClient();
        var provider = new MailgunEmailProvider(
            fakeClient,
            MailgunTestFixtures.Options(defaultDomain: "default.example.com"),
            MailgunTestFixtures.Logger<MailgunEmailProvider>());

        var request = new SendEmailRequest(
            TenantId: Guid.NewGuid(),
            From: "sender@override.com", FromName: null,
            To: OneRecipient, Cc: null, Bcc: null,
            Subject: "hi", HtmlBody: null, TextBody: "hi",
            SendingDomain: "override.example.com");

        await provider.SendAsync(request);

        fakeClient.LastRequest!.Domain.Should().Be("override.example.com");
    }

    [Fact]
    public async Task SendAsync_MissingSendingDomain_Throws()
    {
        var provider = new MailgunEmailProvider(
            new RecordingMailgunClient(),
            MailgunTestFixtures.Options(defaultDomain: null),
            MailgunTestFixtures.Logger<MailgunEmailProvider>());

        var request = new SendEmailRequest(
            TenantId: Guid.NewGuid(),
            From: "s@example.com", FromName: null,
            To: OneRecipient, Cc: null, Bcc: null,
            Subject: "s", HtmlBody: null, TextBody: "t");

        var act = async () => await provider.SendAsync(request);
        await act.Should().ThrowAsync<EmailValidationException>();
    }

    [Fact]
    public async Task SendAsync_NoRecipients_Throws()
    {
        var provider = new MailgunEmailProvider(
            new RecordingMailgunClient(),
            MailgunTestFixtures.Options(defaultDomain: "mg.example.com"),
            MailgunTestFixtures.Logger<MailgunEmailProvider>());

        var request = new SendEmailRequest(
            TenantId: Guid.NewGuid(),
            From: "s@example.com", FromName: null,
            To: NoRecipients, Cc: null, Bcc: null,
            Subject: "s", HtmlBody: null, TextBody: "t");

        await new Func<Task>(async () => await provider.SendAsync(request))
            .Should().ThrowAsync<EmailValidationException>();
    }

    [Fact]
    public async Task SendAsync_MailgunFailure_ReturnsOutcomeWithRetryableFlag()
    {
        var failing = new FailingMailgunClient(
            new MailgunException("Mailgun 503", 503, isRetryable: true, responseBody: "boom"));
        var provider = new MailgunEmailProvider(
            failing,
            MailgunTestFixtures.Options(defaultDomain: "mg.example.com"),
            MailgunTestFixtures.Logger<MailgunEmailProvider>());

        var request = new SendEmailRequest(
            TenantId: Guid.NewGuid(),
            From: "s@example.com", FromName: null,
            To: OneRecipient, Cc: null, Bcc: null,
            Subject: "s", HtmlBody: null, TextBody: "t");

        var outcome = await provider.SendAsync(request);
        outcome.Success.Should().BeFalse();
        outcome.IsRetryable.Should().BeTrue();
        outcome.ErrorCode.Should().Be("mailgun_http_503");
    }

    [Fact]
    public void Capabilities_AdvertisesSendSendRawTagsCustomVariablesAttachments()
    {
        var provider = new MailgunEmailProvider(
            new RecordingMailgunClient(),
            MailgunTestFixtures.Options(defaultDomain: "mg.example.com"),
            MailgunTestFixtures.Logger<MailgunEmailProvider>());

        provider.Capabilities.HasFlag(EmailProviderCapability.Tags).Should().BeTrue();
        provider.Capabilities.HasFlag(EmailProviderCapability.CustomVariables).Should().BeTrue();
        provider.Capabilities.HasFlag(EmailProviderCapability.SendRaw).Should().BeTrue();
        provider.Capabilities.HasFlag(EmailProviderCapability.Attachments).Should().BeTrue();
        provider.ProviderKey.Should().Be(MailgunProviderKey.Value);
    }

    private sealed class RecordingMailgunClient : IMailgunClient
    {
        public MailgunSendRequest? LastRequest { get; private set; }

        public Task<MailgunSendResponse> SendAsync(
            MailgunSendRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new MailgunSendResponse(
                "<20260205213049.abc@mg.example.com>", "Queued. Thank you."));
        }

        public Task<MailgunSendResponse> SendRawAsync(
            string domain, Stream mimeMessage,
            IReadOnlyDictionary<string, string>? customVariables,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MailgunSendResponse("<raw.msg@mg.example.com>", "Queued."));
    }

    private sealed class FailingMailgunClient : IMailgunClient
    {
        private readonly MailgunException _ex;
        public FailingMailgunClient(MailgunException ex) { _ex = ex; }
        public Task<MailgunSendResponse> SendAsync(
            MailgunSendRequest request, CancellationToken cancellationToken = default)
            => throw _ex;
        public Task<MailgunSendResponse> SendRawAsync(
            string domain, Stream mimeMessage,
            IReadOnlyDictionary<string, string>? customVariables,
            CancellationToken cancellationToken = default)
            => throw _ex;
    }
}
