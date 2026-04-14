using SendNex.Mailgun.Dtos;

namespace SendNex.Mailgun;

/// <summary>
/// Interface surface for <see cref="MailgunClient"/> — lets the
/// <c>EaaS.Infrastructure</c> adapter substitute a fake for unit tests
/// without wiring a <see cref="System.Net.Http.HttpMessageHandler"/>.
/// </summary>
public interface IMailgunClient
{
    /// <summary>POST <c>/v3/{domain}/messages</c> with form-encoded fields.</summary>
    Task<MailgunSendResponse> SendAsync(
        MailgunSendRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST <c>/v3/{domain}/messages.mime</c> with a raw RFC-2822 MIME body.</summary>
    Task<MailgunSendResponse> SendRawAsync(
        string domain,
        Stream mimeMessage,
        IReadOnlyDictionary<string, string>? customVariables,
        CancellationToken cancellationToken = default);
}
