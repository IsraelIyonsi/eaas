using EaaS.Api.Features.Inbound.Rules;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Inbound.Rules;

public sealed class ListInboundRulesHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ListInboundRulesHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ListInboundRulesHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new ListInboundRulesHandler(_dbContext);
    }

    [Fact]
    public async Task Should_ReturnPagedRules_ForTenant()
    {
        var domain = SeedDomain("example.com");
        SeedRule(domain.Id, "Rule A", 0);
        SeedRule(domain.Id, "Rule B", 1);
        SeedRule(domain.Id, "Rule C", 2);

        var query = new ListInboundRulesQuery(_tenantId, Page: 1, PageSize: 10, DomainId: null);

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items[0].Name.Should().Be("Rule A");
        result.Items[1].Name.Should().Be("Rule B");
        result.Items[2].Name.Should().Be("Rule C");
    }

    [Fact]
    public async Task Should_FilterByDomain_WhenDomainIdProvided()
    {
        var domain1 = SeedDomain("first.com");
        var domain2 = SeedDomain("second.com");
        SeedRule(domain1.Id, "Rule for First", 0);
        SeedRule(domain2.Id, "Rule for Second", 0);

        var query = new ListInboundRulesQuery(_tenantId, Page: 1, PageSize: 10, DomainId: domain1.Id);

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Rule for First");
    }

    private SendingDomain SeedDomain(string domainName)
    {
        var domain = new SendingDomain
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DomainName = domainName,
            Status = DomainStatus.Verified,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Domains.Add(domain);
        _dbContext.SaveChanges();
        return domain;
    }

    private void SeedRule(Guid domainId, string name, int priority)
    {
        _dbContext.InboundRules.Add(new InboundRule
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DomainId = domainId,
            Name = name,
            MatchPattern = "*@",
            Action = InboundRuleAction.Store,
            IsActive = true,
            Priority = priority,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
