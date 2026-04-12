using MediatR;

namespace EaaS.Api.Features.Emails;

public sealed record GetEmailEventsQuery(Guid TenantId, Guid EmailId) : IRequest<List<EmailEventDto>>;
