using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Utilities;
using MediatR;

namespace EaaS.Api.Features.ApiKeys;

public sealed class CreateApiKeyHandler : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ISubscriptionLimitService _subscriptionLimitService;

    public CreateApiKeyHandler(AppDbContext dbContext, ISubscriptionLimitService subscriptionLimitService)
    {
        _dbContext = dbContext;
        _subscriptionLimitService = subscriptionLimitService;
    }

    public async Task<CreateApiKeyResult> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var canCreate = await _subscriptionLimitService.CanCreateApiKeyAsync(request.TenantId, cancellationToken);
        if (!canCreate)
            throw new QuotaExceededException("Maximum API keys reached. Upgrade your plan.");

        var plaintextKey = GenerateApiKey();
        var keyHash = ComputeSha256Hash(plaintextKey);
        var prefix = plaintextKey[..8];

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Name = request.Name,
            KeyHash = keyHash,
            Prefix = prefix,
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateApiKeyResult(
            apiKey.Id,
            apiKey.Name,
            prefix,
            plaintextKey,
            apiKey.CreatedAt);
    }

    private static string GenerateApiKey() => ApiKeyGenerator.GenerateKey();

    private static string ComputeSha256Hash(string rawKey) => ApiKeyGenerator.ComputeSha256Hash(rawKey);
}
