using System.Text;
using EaaS.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace EaaS.Infrastructure.Tests.Services;

public sealed class MimeKitInboundParserTests
{
    private readonly MimeKitInboundParser _sut = new();

    private const string SimpleMime =
        "From: John <john@example.com>\r\n" +
        "To: support@test.com\r\n" +
        "Subject: Test\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "Hello World";

    private const string MultiRecipientMime =
        "From: sender@example.com\r\n" +
        "To: alice@test.com, Bob <bob@test.com>\r\n" +
        "Cc: carol@test.com\r\n" +
        "Subject: Multi\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "Body";

    private const string HtmlAndTextMime =
        "From: sender@example.com\r\n" +
        "To: recipient@test.com\r\n" +
        "Subject: Mixed\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: multipart/alternative; boundary=\"boundary42\"\r\n" +
        "\r\n" +
        "--boundary42\r\n" +
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "Plain text body\r\n" +
        "--boundary42\r\n" +
        "Content-Type: text/html\r\n" +
        "\r\n" +
        "<p>HTML body</p>\r\n" +
        "--boundary42--";

    private const string WithAttachmentMime =
        "From: sender@example.com\r\n" +
        "To: recipient@test.com\r\n" +
        "Subject: With Attachment\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: multipart/mixed; boundary=\"mixbound\"\r\n" +
        "\r\n" +
        "--mixbound\r\n" +
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "See attached\r\n" +
        "--mixbound\r\n" +
        "Content-Type: application/pdf\r\n" +
        "Content-Disposition: attachment; filename=\"report.pdf\"\r\n" +
        "Content-Transfer-Encoding: base64\r\n" +
        "\r\n" +
        "JVBERi0xLjQKMSAwIG9iago=\r\n" +
        "--mixbound--";

    private const string InReplyToMime =
        "From: replier@example.com\r\n" +
        "To: original@test.com\r\n" +
        "Subject: Re: Original\r\n" +
        "In-Reply-To: <original-msg-id@test.com>\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "This is a reply";

    private const string NoSubjectMime =
        "From: sender@example.com\r\n" +
        "To: recipient@test.com\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "No subject here";

    [Fact]
    public void Should_ParseFrom_Correctly()
    {
        using var stream = ToStream(SimpleMime);
        var result = _sut.Parse(stream);

        result.FromEmail.Should().Be("john@example.com");
        result.FromName.Should().Be("John");
    }

    [Fact]
    public void Should_ParseMultipleRecipients()
    {
        using var stream = ToStream(MultiRecipientMime);
        var result = _sut.Parse(stream);

        result.ToAddresses.Should().HaveCount(2);
        result.ToAddresses[0].Email.Should().Be("alice@test.com");
        result.ToAddresses[1].Email.Should().Be("bob@test.com");
        result.ToAddresses[1].Name.Should().Be("Bob");

        result.CcAddresses.Should().HaveCount(1);
        result.CcAddresses[0].Email.Should().Be("carol@test.com");
    }

    [Fact]
    public void Should_ExtractHtmlAndTextBodies()
    {
        using var stream = ToStream(HtmlAndTextMime);
        var result = _sut.Parse(stream);

        result.TextBody.Should().NotBeNullOrEmpty();
        result.TextBody!.TrimEnd().Should().Be("Plain text body");
        result.HtmlBody.Should().NotBeNullOrEmpty();
        result.HtmlBody!.TrimEnd().Should().Be("<p>HTML body</p>");
    }

    [Fact]
    public void Should_ExtractAttachments()
    {
        using var stream = ToStream(WithAttachmentMime);
        var result = _sut.Parse(stream);

        result.Attachments.Should().HaveCount(1);
        result.Attachments[0].Filename.Should().Be("report.pdf");
        result.Attachments[0].ContentType.Should().Be("application/pdf");
        result.Attachments[0].SizeBytes.Should().BeGreaterThan(0);
        result.Attachments[0].IsInline.Should().BeFalse();
    }

    [Fact]
    public void Should_ExtractInReplyTo_Header()
    {
        using var stream = ToStream(InReplyToMime);
        var result = _sut.Parse(stream);

        // MimeKit strips angle brackets from In-Reply-To header
        result.InReplyTo.Should().Be("original-msg-id@test.com");
        result.Subject.Should().Be("Re: Original");
    }

    [Fact]
    public void Should_HandleMissingSubject()
    {
        using var stream = ToStream(NoSubjectMime);
        var result = _sut.Parse(stream);

        result.Subject.Should().BeNullOrEmpty();
        result.TextBody.Should().Contain("No subject here");
    }

    private static MemoryStream ToStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}
