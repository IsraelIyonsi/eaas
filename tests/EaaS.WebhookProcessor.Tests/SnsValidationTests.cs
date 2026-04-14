using EaaS.WebhookProcessor.Handlers;
using FluentAssertions;
using Xunit;

namespace EaaS.WebhookProcessor.Tests;

public class SnsValidationTests
{
    [Theory]
    [InlineData("https://sns.us-east-1.amazonaws.com/SimpleNotificationService-abc123.pem", true)]
    [InlineData("https://sns.us-east-1.amazonaws.com/SimpleNotificationService-9f8a7e6d.pem", true)]
    [InlineData("https://sns.us-east-1.amazonaws.com/attacker.pem", false)]
    [InlineData("https://sns.us-east-1.amazonaws.com/SimpleNotificationService-abc123.pem/../evil", false)]
    [InlineData("https://sns.us-east-1.amazonaws.com/SimpleNotificationService-.pem", false)]
    [InlineData("https://sns.us-east-1.amazonaws.com/SimpleNotificationService-abc.pem.evil", false)]
    [InlineData("https://sns.us-east-1.amazonaws.com/path/SimpleNotificationService-abc.pem", false)]
    public void IsValidSigningCertUrl_EnforcesAnchoredPathShape(string url, bool expected)
    {
        SnsValidation.IsValidSigningCertUrl(url).Should().Be(expected);
    }
}
