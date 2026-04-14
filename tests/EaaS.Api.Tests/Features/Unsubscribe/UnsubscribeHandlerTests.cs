using EaaS.Api.Features.Unsubscribe;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Unsubscribe;

public sealed class UnsubscribeHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ListUnsubscribeService _tokenService;
    private readonly UnsubscribeHandler _sut;

    public UnsubscribeHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        var settings = Options.Create(new ListUnsubscribeSettings
        {
            HmacSecret = "test-secret-abcdef-01234567",
            BaseUrl = "https://sendnex.xyz",
            MailtoHost = "sendnex.xyz"
        });
        _tokenService = new ListUnsubscribeService(settings);
        _sut = new UnsubscribeHandler(
            _dbContext,
            _tokenService,
            Substitute.For<ILogger<UnsubscribeHandler>>());
    }

    [Fact]
    public async Task Should_SuppressRecipient_When_TokenValid()
    {
        var tenantId = Guid.NewGuid();
        var token = _tokenService.GenerateToken(tenantId, "jane@example.com", DateTime.UtcNow);

        var result = await _sut.Handle(new UnsubscribeCommand(token), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RecipientEmail.Should().Be("jane@example.com");
        result.TenantId.Should().Be(tenantId);

        var stored = await _dbContext.SuppressionEntries
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EmailAddress == "jane@example.com");
        stored.Should().NotBeNull();
        stored!.Reason.Should().Be(SuppressionReason.Manual);
    }

    [Fact]
    public async Task Should_ReturnFailure_When_TokenTampered()
    {
        var tenantId = Guid.NewGuid();
        var token = _tokenService.GenerateToken(tenantId, "jane@example.com", DateTime.UtcNow);
        var tampered = token[..^4] + "zzzz";

        var result = await _sut.Handle(new UnsubscribeCommand(tampered), CancellationToken.None);

        result.Success.Should().BeFalse();
        (await _dbContext.SuppressionEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Should_BeIdempotent_When_CalledTwice()
    {
        var tenantId = Guid.NewGuid();
        var token = _tokenService.GenerateToken(tenantId, "jane@example.com", DateTime.UtcNow);

        var first = await _sut.Handle(new UnsubscribeCommand(token), CancellationToken.None);
        var second = await _sut.Handle(new UnsubscribeCommand(token), CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        (await _dbContext.SuppressionEntries.CountAsync(s => s.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task Should_NormalizeEmail_ToLowercase()
    {
        var tenantId = Guid.NewGuid();
        var token = _tokenService.GenerateToken(tenantId, "Jane@Example.COM", DateTime.UtcNow);

        var result = await _sut.Handle(new UnsubscribeCommand(token), CancellationToken.None);

        result.Success.Should().BeTrue();
        var stored = await _dbContext.SuppressionEntries.FirstAsync();
        stored.EmailAddress.Should().Be("jane@example.com");
    }

    [Fact]
    public async Task Should_ReturnFailure_When_TokenEmpty()
    {
        var result = await _sut.Handle(new UnsubscribeCommand(""), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();
}
