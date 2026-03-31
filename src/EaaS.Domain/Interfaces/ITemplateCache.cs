namespace EaaS.Domain.Interfaces;

public interface ITemplateCache
{
    Task<string?> GetTemplateCacheAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task SetTemplateCacheAsync(Guid templateId, string serializedTemplate, CancellationToken cancellationToken = default);
    Task InvalidateTemplateCacheAsync(Guid templateId, CancellationToken cancellationToken = default);
}
