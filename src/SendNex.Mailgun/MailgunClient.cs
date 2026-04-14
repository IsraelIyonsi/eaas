using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using SendNex.Mailgun.Dtos;

namespace SendNex.Mailgun;

/// <summary>
/// Thin typed <see cref="HttpClient"/> wrapper around the Mailgun v3 REST API.
/// Scope is deliberately minimal — <c>Messages</c> and <c>Messages.mime</c>. Routes,
/// Domains, and Subaccounts live outside Phase 1 and ship in later phases.
/// </summary>
/// <remarks>
/// Resilience (retry/backoff on 429/5xx) is applied by
/// <c>Microsoft.Extensions.Http.Resilience</c> at the DI-registration site, so the
/// client itself stays policy-free and deterministic under unit test.
/// </remarks>
public sealed partial class MailgunClient : IMailgunClient
{
    private readonly HttpClient _http;
    private readonly ILogger<MailgunClient> _logger;

    public MailgunClient(HttpClient http, ILogger<MailgunClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MailgunSendResponse> SendAsync(
        MailgunSendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Domain))
            throw new ArgumentException("Mailgun sending domain is required.", nameof(request));
        if (request.To.Count == 0)
            throw new ArgumentException("At least one recipient is required.", nameof(request));

        using var content = BuildMultipartForm(request);
        var path = BuildMessagesPath(request.Domain);

        return await PostAndParseAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MailgunSendResponse> SendRawAsync(
        string domain,
        Stream mimeMessage,
        IReadOnlyDictionary<string, string>? customVariables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Mailgun sending domain is required.", nameof(domain));
        ArgumentNullException.ThrowIfNull(mimeMessage);

        using var content = new MultipartFormDataContent();
        AppendCustomVariables(content, customVariables);

        var mimeContent = new StreamContent(mimeMessage);
        mimeContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        content.Add(mimeContent, MailgunConstants.FormFields.Message, "message.mime");

        var path = BuildMessagesMimePath(domain);
        return await PostAndParseAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MailgunSendResponse> PostAndParseAsync(
        string path,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(path, content, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogTransportFailure(_logger, ex);
            throw new MailgunException("Transport failure calling Mailgun.", ex, isRetryable: true);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                var parsed = await response.Content
                    .ReadFromJsonAsync<MailgunSendResponse>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (parsed is null || string.IsNullOrEmpty(parsed.Id))
                {
                    throw new MailgunException(
                        "Mailgun returned a success status but the response body was empty or missing 'id'.",
                        (int)response.StatusCode,
                        isRetryable: false);
                }
                return parsed;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var retryable = IsRetryableStatus(response.StatusCode);
            LogNonSuccess(_logger, (int)response.StatusCode, body);
            throw new MailgunException(
                $"Mailgun returned HTTP {(int)response.StatusCode}.",
                (int)response.StatusCode,
                retryable,
                body);
        }
    }

    private static string BuildMessagesPath(string domain) =>
        MailgunConstants.Paths.ApiVersionSegment + domain + MailgunConstants.Paths.MessagesSegment;

    private static string BuildMessagesMimePath(string domain) =>
        MailgunConstants.Paths.ApiVersionSegment + domain + MailgunConstants.Paths.MessagesMimeSegment;

    private static bool IsRetryableStatus(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests
        || ((int)status >= 500 && (int)status <= 599);

    private static MultipartFormDataContent BuildMultipartForm(MailgunSendRequest request)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(request.From, Encoding.UTF8), MailgunConstants.FormFields.From);

        foreach (var to in request.To)
            form.Add(new StringContent(to, Encoding.UTF8), MailgunConstants.FormFields.To);

        if (request.Cc is not null)
            foreach (var cc in request.Cc)
                form.Add(new StringContent(cc, Encoding.UTF8), MailgunConstants.FormFields.Cc);

        if (request.Bcc is not null)
            foreach (var bcc in request.Bcc)
                form.Add(new StringContent(bcc, Encoding.UTF8), MailgunConstants.FormFields.Bcc);

        form.Add(new StringContent(request.Subject, Encoding.UTF8), MailgunConstants.FormFields.Subject);

        if (!string.IsNullOrEmpty(request.Text))
            form.Add(new StringContent(request.Text, Encoding.UTF8), MailgunConstants.FormFields.Text);

        if (!string.IsNullOrEmpty(request.Html))
            form.Add(new StringContent(request.Html, Encoding.UTF8), MailgunConstants.FormFields.Html);

        if (request.Tags is not null)
            foreach (var tag in request.Tags)
                form.Add(new StringContent(tag, Encoding.UTF8), MailgunConstants.FormFields.Tag);

        if (request.TrackingOpens.HasValue)
            form.Add(new StringContent(request.TrackingOpens.Value ? "yes" : "no"),
                MailgunConstants.FormFields.TrackingOpens);

        if (request.TrackingClicks.HasValue)
            form.Add(new StringContent(request.TrackingClicks.Value ? "yes" : "no"),
                MailgunConstants.FormFields.TrackingClicks);

        if (request.CustomHeaders is not null)
            foreach (var header in request.CustomHeaders)
                form.Add(new StringContent(header.Value, Encoding.UTF8),
                    MailgunConstants.FormFields.HeaderPrefix + header.Key);

        AppendCustomVariables(form, request.CustomVariables);

        if (request.Attachments is not null)
            foreach (var attachment in request.Attachments)
            {
                var stream = new StreamContent(attachment.Content);
                stream.Headers.ContentType = new MediaTypeHeaderValue(attachment.ContentType);
                form.Add(stream, MailgunConstants.FormFields.Attachment, attachment.Filename);
            }

        return form;
    }

    private static void AppendCustomVariables(
        MultipartFormDataContent form,
        IReadOnlyDictionary<string, string>? variables)
    {
        if (variables is null) return;
        foreach (var kv in variables)
        {
            form.Add(new StringContent(kv.Value, Encoding.UTF8),
                MailgunConstants.FormFields.VariablePrefix + kv.Key);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Mailgun transport failure")]
    private static partial void LogTransportFailure(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mailgun returned non-success status {StatusCode}: {Body}")]
    private static partial void LogNonSuccess(ILogger logger, int statusCode, string body);
}
