namespace EaaS.Domain.Interfaces;

public interface IIdempotencyStore
{
    Task<string?> GetIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task SetIdempotencyKeyAsync(Guid tenantId, string key, string value, CancellationToken cancellationToken = default);
}
