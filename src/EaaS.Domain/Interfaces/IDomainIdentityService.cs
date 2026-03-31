namespace EaaS.Domain.Interfaces;

public interface IDomainIdentityService
{
    Task<DomainIdentityResult> CreateDomainIdentityAsync(string domain, CancellationToken cancellationToken = default);
    Task<DomainVerificationResult> GetDomainVerificationStatusAsync(string domain, CancellationToken cancellationToken = default);
    Task DeleteDomainIdentityAsync(string domain, CancellationToken cancellationToken = default);
}
