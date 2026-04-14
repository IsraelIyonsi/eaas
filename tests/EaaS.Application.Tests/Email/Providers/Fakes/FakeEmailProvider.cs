using EaaS.Application.Email.Providers;
using FluentValidation;

namespace EaaS.Application.Tests.Email.Providers.Fakes;

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

    public string ProviderName => "fake";

    public ProviderCapabilities Capabilities { get; } = new(
        SupportsAttachments: true,
        SupportsCustomVariables: true,
        SupportsTags: true,
        SupportsNonces: false,
        MaxRecipients: 50,
        MaxAttachmentBytes: 10 * 1024 * 1024);

    public Task<SendEmailResult> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Validate(request);

        cancellationToken.ThrowIfCancellationRequested();

        switch (NextMode)
        {
            case Mode.InvalidFromAddress:
                return Task.FromResult(SendEmailResult.PermanentFailure(
                    "invalid_from_address",
                    $"From address '{request.From}' is not a valid RFC 5322 mailbox."));

            case Mode.ServerError5xx:
                return Task.FromResult(SendEmailResult.TransientFailure(
                    "provider_5xx",
                    "Upstream provider returned HTTP 503."));

            case Mode.ClientError4xx:
                return Task.FromResult(SendEmailResult.PermanentFailure(
                    "provider_4xx",
                    "Upstream provider returned HTTP 400."));

            case Mode.Success:
            default:
                SentRequests.Add(request);
                return Task.FromResult(SendEmailResult.Success($"fake-{Guid.NewGuid():N}"));
        }
    }

    private static void Validate(SendEmailRequest request)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        if (request.To is null || request.To.Count == 0)
        {
            failures.Add(new FluentValidation.Results.ValidationFailure(nameof(request.To), "At least one recipient is required."));
        }

        if (string.IsNullOrWhiteSpace(request.From))
        {
            failures.Add(new FluentValidation.Results.ValidationFailure(nameof(request.From), "From is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            failures.Add(new FluentValidation.Results.ValidationFailure(nameof(request.Subject), "Subject is required."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
