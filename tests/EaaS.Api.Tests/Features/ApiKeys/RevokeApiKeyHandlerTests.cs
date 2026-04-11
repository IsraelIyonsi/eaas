using EaaS.Api.Features.ApiKeys;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.ApiKeys;

public sealed class RevokeApiKeyHandlerTests
{
    private readonly IApiKeyCache _apiKeyCache = Substitute.For<IApiKeyCache>();
    private readonly ILogger<RevokeApiKeyHandler> _logger = Substitute.For<ILogger<RevokeApiKeyHandler>>();

    [Fact]
    public async Task Should_RevokeKey_WhenExists()
    {
        var tenantId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var apiKey = new ApiKey
        {
            Id = keyId,
            TenantId = tenantId,
            Name = "Test Key",
            KeyHash = "hash123",
            Prefix = "eaas_liv",
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var apiKeys = new List<ApiKey> { apiKey };
        var dbContext = CreateMockDbContext(apiKeys);
        var sut = new RevokeApiKeyHandler(dbContext, _apiKeyCache, _logger);

        var command = new RevokeApiKeyCommand(keyId, tenantId);

        await sut.Handle(command, CancellationToken.None);

        apiKey.Status.Should().Be(ApiKeyStatus.Revoked);
        apiKey.RevokedAt.Should().NotBeNull();
        await _apiKeyCache.Received(1).InvalidateApiKeyCacheAsync("hash123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenKeyNotFound()
    {
        var apiKeys = new List<ApiKey>();
        var dbContext = CreateMockDbContext(apiKeys);
        var sut = new RevokeApiKeyHandler(dbContext, _apiKeyCache, _logger);

        var command = new RevokeApiKeyCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenKeyBelongsToDifferentTenant()
    {
        var keyId = Guid.NewGuid();
        var apiKey = new ApiKey
        {
            Id = keyId,
            TenantId = Guid.NewGuid(),
            Name = "Test Key",
            KeyHash = "hash123",
            Prefix = "eaas_liv",
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var apiKeys = new List<ApiKey> { apiKey };
        var dbContext = CreateMockDbContext(apiKeys);
        var sut = new RevokeApiKeyHandler(dbContext, _apiKeyCache, _logger);

        var differentTenantId = Guid.NewGuid();
        var command = new RevokeApiKeyCommand(keyId, differentTenantId);

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static AppDbContext CreateMockDbContext(List<ApiKey> apiKeys)
    {
        var mockApiKeys = MockDbSetFactory.Create(apiKeys);

        var dbContext = Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        dbContext.ApiKeys.Returns(mockApiKeys);

        return dbContext;
    }
}
