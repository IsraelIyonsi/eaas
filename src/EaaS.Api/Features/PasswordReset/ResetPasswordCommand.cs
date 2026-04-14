using MediatR;

namespace EaaS.Api.Features.PasswordReset;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<ResetPasswordResult>;

public sealed record ResetPasswordResult(bool Success);
