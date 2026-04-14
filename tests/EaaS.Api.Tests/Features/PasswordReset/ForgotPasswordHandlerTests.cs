using EaaS.Api.Features.PasswordReset;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.PasswordReset;

public sealed class ForgotPasswordHandlerTests
{
    private readonly ILogger<ForgotPasswordHandler> _logger = Substitute.For<ILogger<ForgotPasswordHandler>>();
    private readonly IPasswordResetEmailSender _emailSender = Substitute.For<IPasswordResetEmailSender>();
    private readonly InMemoryPasswordResetTokenStore _tokenStore = new();
    private readonly PasswordResetSettings _settings = new()
    {
        HmacSecret = "test-secret-must-be-long-enough",
        DashboardBaseUrl = "http://localhost:3000",
        SystemSender = "sendnex-ops@sendnex.xyz",
        TokenLifetimeMinutes = 30,
        MaxRequestsPerEmailPerHour = 3,
        MaxRequestsPerIpPerHour = 3,
    };

    private ForgotPasswordHandler CreateSut(AppDbContext dbContext)
        => new(
            dbContext,
            _tokenStore,
            new PasswordResetTokenService(Options.Create(_settings)),
            _emailSender,
            Options.Create(_settings),
            _logger);

    [Fact]
    public async Task Should_SendEmail_WhenEmailMatchesExistingTenant()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("user@example.com")
            .Build();

        var dbContext = CreateMockDbContext(new List<Tenant> { tenant });
        var sut = CreateSut(dbContext);

        var command = new ForgotPasswordCommand("user@example.com", "1.2.3.4");

        var result = await sut.Handle(command, CancellationToken.None);

        result.Accepted.Should().BeTrue();
        _tokenStore.Tokens.Should().HaveCount(1);
        _tokenStore.Tokens.Values.First().TenantId.Should().Be(tenant.Id);

        await _emailSender.Received(1).SendResetEmailAsync(
            "user@example.com",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnSuccess_AndNotSendEmail_WhenEmailUnknown()
    {
        var dbContext = CreateMockDbContext(new List<Tenant>());
        var sut = CreateSut(dbContext);

        var command = new ForgotPasswordCommand("nobody@example.com", "1.2.3.4");

        var result = await sut.Handle(command, CancellationToken.None);

        result.Accepted.Should().BeTrue();
        _tokenStore.Tokens.Should().BeEmpty();
        await _emailSender.DidNotReceive().SendResetEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_MatchEmail_CaseInsensitive()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("User@Example.COM")
            .Build();

        var dbContext = CreateMockDbContext(new List<Tenant> { tenant });
        var sut = CreateSut(dbContext);

        var command = new ForgotPasswordCommand("user@example.com", null);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Accepted.Should().BeTrue();
        _tokenStore.Tokens.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_SilentlyDrop_WhenEmailRateLimitExceeded()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("user@example.com")
            .Build();

        var dbContext = CreateMockDbContext(new List<Tenant> { tenant });
        var sut = CreateSut(dbContext);

        var command = new ForgotPasswordCommand("user@example.com", "9.9.9.9");

        // Hit up to the limit — all send.
        for (var i = 0; i < _settings.MaxRequestsPerEmailPerHour; i++)
        {
            await sut.Handle(command, CancellationToken.None);
        }

        _emailSender.ClearReceivedCalls();

        // Next hit must be silently dropped (no email, no token stored).
        var priorTokens = _tokenStore.Tokens.Count;
        var result = await sut.Handle(command, CancellationToken.None);

        result.Accepted.Should().BeTrue();
        _tokenStore.Tokens.Count.Should().Be(priorTokens);
        await _emailSender.DidNotReceive().SendResetEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SwallowEmailSenderException_AndStillReturnSuccess()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("user@example.com")
            .Build();

        var dbContext = CreateMockDbContext(new List<Tenant> { tenant });
        _emailSender
            .SendResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SES down")));

        var sut = CreateSut(dbContext);

        var command = new ForgotPasswordCommand("user@example.com", null);
        var result = await sut.Handle(command, CancellationToken.None);

        result.Accepted.Should().BeTrue();
    }

    private static AppDbContext CreateMockDbContext(List<Tenant> tenants)
    {
        var mockTenants = MockDbSetFactory.Create(tenants);

        var dbContext = Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        dbContext.Tenants.Returns(mockTenants);
        return dbContext;
    }
}
