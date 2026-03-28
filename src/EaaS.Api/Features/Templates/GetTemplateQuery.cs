using MediatR;

namespace EaaS.Api.Features.Templates;

public sealed record GetTemplateQuery(Guid TenantId, Guid TemplateId) : IRequest<TemplateResult>;
