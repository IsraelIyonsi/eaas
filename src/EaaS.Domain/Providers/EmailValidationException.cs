namespace EaaS.Domain.Providers;

/// <summary>
/// Thrown by <see cref="IEmailProvider"/> implementations when a <see cref="SendEmailRequest"/>
/// fails domain-level validation (missing recipients, invalid sender, etc.). The Domain
/// project MUST NOT take a dependency on FluentValidation — this exception is the
/// provider-agnostic equivalent.
/// </summary>
public sealed class EmailValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public EmailValidationException(string message) : base(message)
    {
        Errors = new[] { message };
    }

    public EmailValidationException(IReadOnlyList<string> errors)
        : base(errors.Count == 0 ? "Email validation failed." : string.Join("; ", errors))
    {
        Errors = errors;
    }

    public EmailValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = new[] { message };
    }
}
