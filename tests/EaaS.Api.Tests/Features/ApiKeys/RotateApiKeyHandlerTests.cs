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

public sealed class RotateApiKeyHandlerTests
{
    private readonly IApiKeyCache _apiKeyCache = Substitute.For<IApiKeyCache>();
    private readonly ILogger<RotateApiKeyHandler> _logger = Substitute.For<ILogger<RotateApiKeyHandler>>();

    [Fact]
    public async Task Should_GenerateNewKey_AndDeactivateOld()
    {
        var tenantId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var existingKey = new ApiKey
        {
            Id = keyId,
            TenantId = tenantId,
            Name = "Original Key",
            KeyHash = "oldhash123",
            Prefix = "eaas_liv",
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var apiKeys = new List<ApiKey> { existingKey };
        var dbContext = CreateMockDbContext(apiKeys);
        var sut = new RotateApiKeyHandler(dbContext, _apiKeyCache, _logger);

        var command = new RotateApiKeyCommand(keyId, tenantId);

        var result = await sut.Handle(command, CancellationToken.None);

        existingKey.Status.Should().Be(ApiKeyStatus.Rotating);
        existingKey.RotatingExpiresAt.Should().NotBeNull();
        existingKey.ReplacedByKeyId.Should().Be(result.KeyId);

        apiKeys.Should().HaveCount(2);
        var newKey = apiKeys.First(k => k.Id == result.KeyId);
        newKey.Status.Should().Be(ApiKeyStatus.Active);
        newKey.Name.Should().Be("Original Key (rotated)");
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenKeyNotFound()
    {
        var apiKeys = new List<ApiKey>();
        var dbContext = CreateMockDbContext(apiKeys);
        var sut = new RotateApiKeyHandler(dbContext, _apiKeyCache, _logger);

        var command = new RotateApiKeyCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_ReturnNewKeyValue()
    {
        var tenantId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var existingKey = new ApiKey
        {
            Id = keyId,
            TenantId = tenantId,
            Name = "Original Key",
            KeyHash = "oldhash123",
            Prefix = "eaas_liv",
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var apiKeys = new List<ApiKey> { existingKey };
        var dbContext = CreateMockDbContext(apiKeys);
        var sut = new RotateApiKeyHandler(dbContext, _apiKeyCache, _logger);

        var command = new RotateApiKeyCommand(keyId, tenantId);

        var result = await sut.Handle(command, CancellationToken.None);

        result.ApiKey.Should().NotBeNullOrEmpty();
        result.ApiKey.Should().StartWith("eaas_live_");
        result.Prefix.Should().Be(result.ApiKey[..8]);
        result.KeyId.Should().NotBeEmpty();
        result.OldKeyExpiresAt.Should().BeAfter(DateTime.UtcNow);
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
