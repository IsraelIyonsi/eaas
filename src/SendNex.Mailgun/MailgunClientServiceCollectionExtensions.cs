using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace SendNex.Mailgun;

/// <summary>
/// DI surface for the <see cref="SendNex.Mailgun"/> library. Registers the typed
/// <see cref="IMailgunClient"/>, binds <see cref="MailgunOptions"/> from the
/// <c>EmailProviders:Mailgun</c> section, and layers
/// <see cref="Microsoft.Extensions.Http.Resilience"/> handlers for 429/5xx retries.
/// </summary>
public static class MailgunClientServiceCollectionExtensions
{
    /// <summary>
    /// Wires the typed Mailgun HTTP client + options binding. Call site:
    /// <c>services.AddMailgunHttpClient(configuration)</c>. No other Infrastructure
    /// plumbing leaks into <c>SendNex.Mailgun</c>.
    /// </summary>
    public static IServiceCollection AddMailgunHttpClient(
        this IServiceCollection services,
        string configSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(configSectionName);

        services.AddOptions<MailgunOptions>()
            .BindConfiguration(configSectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddHttpClient<IMailgunClient, MailgunClient>(MailgunConstants.HttpClientName,
                (sp, client) =>
                {
                    var opts = sp.GetRequiredService<IOptions<MailgunOptions>>().Value;
                    client.BaseAddress = new Uri(opts.ApiBaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

                    var credentials = Convert.ToBase64String(
                        Encoding.ASCII.GetBytes($"{MailgunConstants.BasicAuthUser}:{opts.ApiKey}"));
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Basic", credentials);
                })
            .AddResilienceHandler("mailgun-retries", builder =>
            {
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(500),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(r =>
                            r.StatusCode == HttpStatusCode.TooManyRequests ||
                            ((int)r.StatusCode >= 500 && (int)r.StatusCode <= 599))
                });
            });

        return services;
    }
}
