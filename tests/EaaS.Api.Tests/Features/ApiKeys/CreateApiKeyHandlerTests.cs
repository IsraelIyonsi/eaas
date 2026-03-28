using EaaS.Api.Features.ApiKeys;
using EaaS.Api.Tests.Helpers;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EaaS.Api.Tests.Features.ApiKeys;

public sealed class CreateApiKeyHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CreateApiKeyHandler _sut;

    public CreateApiKeyHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new CreateApiKeyHandler(_dbContext);
    }

    [Fact]
    public async Task Should_CreateKeyWithHashedValue_When_Valid()
    {
        var command = TestDataBuilders.CreateApiKey().Build();

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be(command.Name);
        result.Key.Should().StartWith("eaas_live_");
        result.KeyPrefix.Should().Be(result.Key[..8]);

        var storedKey = await _dbContext.ApiKeys.FirstOrDefaultAsync();
        storedKey.Should().NotBeNull();
        storedKey!.KeyHash.Should().NotBeNullOrEmpty();
        storedKey.KeyHash.Should().NotBe(result.Key, "the stored hash must differ from the plaintext key");
    }

    [Fact]
    public async Task Should_ReturnPlaintextKeyOnce()
    {
        var command = TestDataBuilders.CreateApiKey().Build();

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Key.Should().NotBeNullOrEmpty();
        result.Key.Length.Should().BeGreaterThan(10);
        result.Key.Should().StartWith("eaas_live_");

        // Verify the key is 50 chars total (10 prefix + 40 random)
        result.Key.Should().HaveLength(50);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
