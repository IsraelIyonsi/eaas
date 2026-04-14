namespace EaaS.Domain.Providers;

/// <summary>
/// Per-provider webhook payload normalizer. Translates a provider-shaped payload
/// (SES-via-SNS JSON, Mailgun multipart form, etc.) into one or more canonical
/// <see cref="ProviderEmailEvent"/> records. See architect doc §7 mapping table.
/// </summary>
public interface IEmailEventNormalizer
{
    string ProviderKey { get; }

    Task<IReadOnlyList<ProviderEmailEvent>> NormalizeAsync(
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
