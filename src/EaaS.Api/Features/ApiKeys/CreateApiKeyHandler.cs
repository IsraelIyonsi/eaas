using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Utilities;
using MediatR;

namespace EaaS.Api.Features.ApiKeys;

public sealed class CreateApiKeyHandler : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResult>
{
    private readonly AppDbContext _dbContext;

    public CreateApiKeyHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateApiKeyResult> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
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
