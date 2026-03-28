using MediatR;

namespace EaaS.Api.Features.Suppressions;

public sealed record AddSuppressionCommand(
    Guid TenantId,
    string EmailAddress) : IRequest<AddSuppressionResult>;

public sealed record AddSuppressionResult(
    Guid Id,
    string EmailAddress,
    string Reason,
    DateTime SuppressedAt);
