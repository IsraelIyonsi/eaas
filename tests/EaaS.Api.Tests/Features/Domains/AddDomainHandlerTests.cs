using EaaS.Api.Features.Domains;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Domains;

public sealed class AddDomainHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IDomainIdentityService _emailDeliveryService;
    private readonly AddDomainHandler _sut;

    public AddDomainHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _emailDeliveryService = Substitute.For<IDomainIdentityService>();
        _sut = new AddDomainHandler(_dbContext, _emailDeliveryService);
    }

    [Fact]
    public async Task Should_CallSesCreateIdentity_When_ValidDomain()
    {
        var command = TestDataBuilders.AddDomain()
            .WithDomainName("newdomain.com")
            .Build();

        _emailDeliveryService.CreateDomainIdentityAsync("newdomain.com", Arg.Any<CancellationToken>())
            .Returns(new DomainIdentityResult(
                true,
                "arn:aws:ses:us-east-1:123:identity/newdomain.com",
                new List<DkimToken> { new("token1"), new("token2"), new("token3") },
                null));

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.DomainName.Should().Be("newdomain.com");
        result.Status.Should().Be("PendingVerification");

        await _emailDeliveryService.Received(1)
            .CreateDomainIdentityAsync("newdomain.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_StoreDnsRecords_When_SesReturns()
    {
        var command = TestDataBuilders.AddDomain()
            .WithDomainName("store-dns.com")
            .Build();

        _emailDeliveryService.CreateDomainIdentityAsync("store-dns.com", Arg.Any<CancellationToken>())
            .Returns(new DomainIdentityResult(
                true,
                null,
                new List<DkimToken> { new("dkim1"), new("dkim2") },
                null));

        var result = await _sut.Handle(command, CancellationToken.None);

        // Should have SPF + 2 DKIM + DMARC = 4 records
        result.DnsRecords.Should().HaveCount(4);
        result.DnsRecords.Should().Contain(r => r.Purpose == "spf");
        result.DnsRecords.Should().Contain(r => r.Purpose == "dkim");
        result.DnsRecords.Should().Contain(r => r.Purpose == "dmarc");

        var savedDomain = await _dbContext.Domains
            .Include(d => d.DnsRecords)
            .FirstOrDefaultAsync(d => d.DomainName == "store-dns.com");

        savedDomain.Should().NotBeNull();
        savedDomain!.DnsRecords.Should().HaveCount(4);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
