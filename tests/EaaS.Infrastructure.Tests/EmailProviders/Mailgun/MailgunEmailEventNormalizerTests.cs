using System.Text;
using EaaS.Domain.Providers;
using EaaS.Infrastructure.EmailProviders.Providers.Mailgun;
using FluentAssertions;
using Xunit;

namespace EaaS.Infrastructure.Tests.EmailProviders.Mailgun;

public sealed class MailgunEmailEventNormalizerTests
{
    private static readonly Dictionary<string, string> NoHeaders = new();

    private static MailgunEmailEventNormalizer Create() => new(
        MailgunTestFixtures.Logger<MailgunEmailEventNormalizer>());

    [Theory]
    [InlineData("delivered", null, EmailEventType.Delivered)]
    [InlineData("accepted", null, EmailEventType.Accepted)]
    [InlineData("opened", null, EmailEventType.Opened)]
    [InlineData("clicked", null, EmailEventType.Clicked)]
    [InlineData("complained", null, EmailEventType.Complained)]
    [InlineData("unsubscribed", null, EmailEventType.Unsubscribed)]
    [InlineData("rejected", null, EmailEventType.PermFailed)]
    [InlineData("stored", null, EmailEventType.Stored)]
    [InlineData("failed", "permanent", EmailEventType.Bounced)]
    [InlineData("failed", "temporary", EmailEventType.TempFailed)]
    public async Task NormalizeAsync_MapsEveryKnownEvent(
        string eventName, string? severity, EmailEventType expected)
    {
        var payload = BuildPayload(eventName, severity, "msg-id-1", tenantId: "t-abc");
        var result = await Create().NormalizeAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);

        result.Should().HaveCount(1);
        result[0].Type.Should().Be(expected);
        result[0].ProviderKey.Should().Be(MailgunProviderKey.Value);
        result[0].ProviderMessageId.Should().Be("msg-id-1");
        result[0].ProviderMetadata.Should().ContainKey("user-variables.tenant_id");
        result[0].ProviderMetadata["user-variables.tenant_id"].Should().Be("t-abc");
    }

    [Fact]
    public async Task NormalizeAsync_MissingTenantId_StillReturnsEvent()
    {
        var payload = BuildPayload("delivered", severity: null, messageId: "msg-id-2", tenantId: null);
        var result = await Create().NormalizeAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);

        result.Should().HaveCount(1);
        result[0].ProviderMetadata.Should().NotContainKey("user-variables.tenant_id");
    }

    [Fact]
    public async Task NormalizeAsync_MalformedJson_ReturnsEmpty()
    {
        var result = await Create().NormalizeAsync(Encoding.UTF8.GetBytes("{broken"), NoHeaders);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task NormalizeAsync_UnknownEvent_ReturnsEmpty()
    {
        var payload = BuildPayload("some-future-event", severity: null, messageId: "m", tenantId: null);
        var result = await Create().NormalizeAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task NormalizeAsync_MissingMessageId_ReturnsEmpty()
    {
        const string payload = """
          {
            "event-data": {
              "event": "delivered",
              "recipient": "user@example.com"
            }
          }
          """;
        var result = await Create().NormalizeAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task NormalizeAsync_NoEventData_ReturnsEmpty()
    {
        const string payload = """{"signature":{}}""";
        var result = await Create().NormalizeAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task NormalizeAsync_UnixTimestamp_IsPreserved()
    {
        var payload = BuildPayload("delivered", severity: null, messageId: "m", tenantId: null, timestamp: 1700000000);
        var result = await Create().NormalizeAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);

        result.Should().HaveCount(1);
        result[0].OccurredAt.ToUnixTimeSeconds().Should().Be(1700000000);
    }

    private static string BuildPayload(
        string eventName,
        string? severity,
        string messageId,
        string? tenantId,
        long? timestamp = null)
    {
        var severityLine = severity is null ? "" : $@",""severity"":""{severity}""";
        var userVarsLine = tenantId is null ? "" : $@",""user-variables"":{{""tenant_id"":""{tenantId}""}}";
        var tsLine = timestamp is null ? "" : $@",""timestamp"":{timestamp.Value}";
        return $$"""
        {
          "event-data": {
            "event": "{{eventName}}"{{severityLine}}{{tsLine}},
            "recipient": "user@example.com",
            "message": { "headers": { "message-id": "{{messageId}}" } }
            {{userVarsLine}}
          }
        }
        """;
    }
}
