using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.AuditLogs;

public sealed record ListAuditLogsQuery(
    int Page,
    int PageSize,
    string? Action,
    Guid? AdminUserId,
    DateTime? From,
    DateTime? To) : IRequest<PagedResponse<AuditLogResult>>;
