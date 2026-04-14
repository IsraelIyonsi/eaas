using EaaS.Domain.Providers;

namespace EaaS.Domain.Providers.Tests.Email.Providers.Fakes;

/// <summary>
/// Reference implementation of <see cref="IEmailProvider"/> used ONLY by the contract-test
/// suite to prove the abstract base class compiles and its assertions hold against a
/// known-good provider. Not for production use.
/// </summary>
public sealed class FakeEmailProvider : IEmailProvider
{
    public enum Mode
    {
        Success,
        InvalidFromAddress,
        ServerError5xx,
        ClientError4xx,
    }

    public Mode NextMode { get; set; } = Mode.Success;

    public List<SendEmailRequest> SentRequests { get; } = new();

    public string ProviderKey => "fake";

    public EmailProviderCapability Capabilities =>
        EmailProviderCapability.Attachments
        | EmailProviderCapability.Tags
        | EmailProviderCapability.CustomVariables
        | EmailProviderCapability.SendRaw;

    public Task<EmailSendOutcome> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Validate(request);

        cancellationToken.ThrowIfCancellationRequested();

        switch (NextMode)
        {
            case Mode.InvalidFromAddress:
                return Task.FromResult(new EmailSendOutcome(
                    Success: false,
                    ProviderMessageId: null,
                    ErrorCode: "invalid_from_address",
                    ErrorMessage: $"From address '{request.From}' is not a valid RFC 5322 mailbox.",
                    IsRetryable: false));

            case Mode.ServerError5xx:
                return Task.FromResult(new EmailSendOutcome(
                    Success: false,
                    ProviderMessageId: null,
                    ErrorCode: "provider_5xx",
                    ErrorMessage: "Upstream provider returned HTTP 503.",
                    IsRetryable: true));

            case Mode.ClientError4xx:
                return Task.FromResult(new EmailSendOutcome(
                    Success: false,
                    ProviderMessageId: null,
                    ErrorCode: "provider_4xx",
                    ErrorMessage: "Upstream provider returned HTTP 400.",
                    IsRetryable: false));

            case Mode.Success:
            default:
                SentRequests.Add(request);
                return Task.FromResult(new EmailSendOutcome(
                    Success: true,
                    ProviderMessageId: $"fake-{Guid.NewGuid():N}",
                    ErrorCode: null,
                    ErrorMessage: null,
                    IsRetryable: false));
        }
    }

    public Task<EmailSendOutcome> SendRawAsync(SendRawEmailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new EmailSendOutcome(
            Success: true,
            ProviderMessageId: $"fake-raw-{Guid.NewGuid():N}",
            ErrorCode: null,
            ErrorMessage: null,
            IsRetryable: false));
    }

    private static void Validate(SendEmailRequest request)
    {
        var errors = new List<string>();
        if (request.To is null || request.To.Count == 0)
        {
            errors.Add("At least one recipient is required.");
        }

        if (string.IsNullOrWhiteSpace(request.From))
        {
            errors.Add("From is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            errors.Add("Subject is required.");
        }

        if (errors.Count > 0)
        {
            throw new EmailValidationException(errors);
        }
    }
}
