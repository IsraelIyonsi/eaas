using MediatR;

namespace EaaS.Api.Features.Unsubscribe;

/// <summary>
/// Marks a recipient as suppressed in response to a List-Unsubscribe click
/// (RFC 8058 One-Click) or header mailto. Idempotent.
/// </summary>
public sealed record UnsubscribeCommand(string Token) : IRequest<UnsubscribeResult>;

public sealed record UnsubscribeResult(bool Success, string? RecipientEmail, Guid? TenantId);
