using MediatR;

namespace EaaS.Api.Features.PasswordReset;

/// <summary>
/// Requests a password reset email for the given address.
/// Always succeeds (returns 200) — even if the email is unknown — to prevent enumeration.
/// </summary>
public sealed record ForgotPasswordCommand(string Email, string? ClientIp) : IRequest<ForgotPasswordResult>;

public sealed record ForgotPasswordResult(bool Accepted);
