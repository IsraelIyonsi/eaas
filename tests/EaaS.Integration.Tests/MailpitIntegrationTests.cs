using System.Net;
using System.Net.Mail;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace EaaS.Integration.Tests;

/// <summary>
/// Integration tests that verify outbound email flow through Mailpit.
/// Requires Docker services running: docker compose --profile local up -d
/// Mailpit Web UI: http://localhost:8025
/// Mailpit SMTP: localhost:1025
/// </summary>
[Trait("Category", "Integration")]
public sealed class MailpitIntegrationTests
{
    private const string MailpitApiUrl = "http://localhost:8025/api/v1";
    private const string MailpitSmtpHost = "localhost";
    private const int MailpitSmtpPort = 1025;

    private readonly ITestOutputHelper _output;

    public MailpitIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Should_SendEmail_AndAppearInMailpit()
    {
        if (!await IsMailpitReachable())
        {
            _output.WriteLine("SKIPPED: Mailpit not reachable. Run: docker compose --profile local up -d");
            return;
        }

        var subject = $"EaaS Test - {Guid.NewGuid():N}";
        using var smtpClient = new SmtpClient(MailpitSmtpHost, MailpitSmtpPort)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress("test@eaas.local", "EaaS Test"),
            Subject = subject,
            Body = "<h1>Integration Test</h1><p>Sent by the EaaS integration test suite.</p>",
            IsBodyHtml = true
        };
        mailMessage.To.Add(new MailAddress("recipient@example.com"));

        await smtpClient.SendMailAsync(mailMessage);

        await Task.Delay(1000);

        using var httpClient = new HttpClient();
        var searchUrl = $"{MailpitApiUrl}/search?query=subject:{Uri.EscapeDataString(subject)}";
        var response = await httpClient.GetAsync(searchUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var messages = doc.RootElement.GetProperty("messages");
        messages.GetArrayLength().Should().BeGreaterThan(0,
            "the email should appear in Mailpit after sending via SMTP");

        var firstMessage = messages[0];
        firstMessage.GetProperty("Subject").GetString().Should().Be(subject);
    }

    [Fact]
    public async Task Should_VerifyMailpitReceivesMultipleEmails()
    {
        if (!await IsMailpitReachable())
        {
            _output.WriteLine("SKIPPED: Mailpit not reachable. Run: docker compose --profile local up -d");
            return;
        }

        var batchId = Guid.NewGuid().ToString("N")[..8];
        using var smtpClient = new SmtpClient(MailpitSmtpHost, MailpitSmtpPort)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        // Send 3 emails
        for (var i = 1; i <= 3; i++)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress("batch@eaas.local", "EaaS Batch Test"),
                Subject = $"Batch {batchId} - Email {i}",
                Body = $"<p>Batch email {i} of 3</p>",
                IsBodyHtml = true
            };
            mailMessage.To.Add(new MailAddress($"user{i}@example.com"));
            await smtpClient.SendMailAsync(mailMessage);
        }

        await Task.Delay(2000);

        using var httpClient = new HttpClient();
        var searchUrl = $"{MailpitApiUrl}/search?query=subject:Batch+{batchId}";
        var response = await httpClient.GetAsync(searchUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var messages = doc.RootElement.GetProperty("messages");
        messages.GetArrayLength().Should().Be(3,
            "all 3 batch emails should appear in Mailpit");
    }

    private static async Task<bool> IsMailpitReachable()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            var response = await httpClient.GetAsync($"{MailpitApiUrl}/messages");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
