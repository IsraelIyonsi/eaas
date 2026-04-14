namespace EaaS.Domain.Interfaces;

/// <summary>
/// Outcome of a single email send attempt through an <see cref="IEmailProvider"/> implementation.
/// </summary>
public record SendEmailResult(bool Success, string? MessageId, string? ErrorMessage);
