namespace EaaS.Api.Features.Admin.AuditLogs;

public sealed record AuditLogResult(
    Guid Id,
    Guid AdminUserId,
    string AdminEmail,
    string Action,
    string? TargetType,
    string? TargetId,
    string Details,
    string? IpAddress,
    DateTime CreatedAt);
