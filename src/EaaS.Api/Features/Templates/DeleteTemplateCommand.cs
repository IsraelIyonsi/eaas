using MediatR;

namespace EaaS.Api.Features.Templates;

public sealed record DeleteTemplateCommand(Guid TenantId, Guid TemplateId) : IRequest;
