namespace EaaS.Application.Email.Providers;

/// <summary>
/// Translates a provider-specific webhook payload into zero-or-more normalized
/// <see cref="EmailEvent"/> records. Unknown event types MUST NOT throw — they
/// should be logged and omitted from the output (yield break).
/// </summary>
public interface IEmailEventNormalizer
{
    string ProviderName { get; }

    IAsyncEnumerable<EmailEvent> NormalizeAsync(
        ReadOnlyMemory<byte> rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
