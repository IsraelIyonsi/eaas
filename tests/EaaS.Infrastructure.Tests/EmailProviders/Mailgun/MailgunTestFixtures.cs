using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SendNex.Mailgun;

namespace EaaS.Infrastructure.Tests.EmailProviders.Mailgun;

/// <summary>
/// Shared builders for the Mailgun adapter test suite. Keeps the individual test
/// files declarative and focused on the behaviour under test.
/// </summary>
internal static class MailgunTestFixtures
{
    public const string SigningKey = "key-test-signing-not-a-real-secret-xxxxxxxxxxxx";
    public const string ApiKey = "key-test-api-not-a-real-secret";

    public static IOptions<MailgunOptions> Options(
        string? signingKey = null,
        string? defaultDomain = null)
    {
        return Microsoft.Extensions.Options.Options.Create(new MailgunOptions
        {
            ApiKey = ApiKey,
            ApiBaseUrl = MailgunConstants.DefaultApiBaseUrl,
            WebhookSigningKey = signingKey ?? SigningKey,
            DefaultRegion = MailgunConstants.Regions.Us,
            DefaultSendingDomain = defaultDomain,
            Enabled = true
        });
    }

    public static NullLogger<T> Logger<T>() => NullLogger<T>.Instance;

    public static string Hmac(string key, string message)
    {
        var bytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message));
        return Convert.ToHexStringLower(bytes);
    }

    public static string UnixNow(TimeProvider? clock = null) =>
        (clock ?? TimeProvider.System).GetUtcNow().ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
}
