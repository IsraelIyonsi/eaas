using MediatR;

namespace EaaS.Api.Features.Templates;

public sealed record RollbackTemplateCommand(
    Guid TenantId,
    Guid TemplateId,
    int TargetVersion) : IRequest<TemplateResult>;
