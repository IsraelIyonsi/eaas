using EaaS.Api.Features.Inbound.Rules;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Inbound.Rules;

public sealed class UpdateInboundRuleHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly UpdateInboundRuleHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public UpdateInboundRuleHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new UpdateInboundRuleHandler(_dbContext);
    }

    [Fact]
    public async Task Should_UpdateName_Successfully()
    {
        var domain = SeedDomain();
        var rule = SeedRule(domain.Id);

        var command = new UpdateInboundRuleCommand(
            _tenantId,
            rule.Id,
            Name: "Updated Rule Name",
            MatchPattern: null,
            Action: null,
            WebhookUrl: null,
            ForwardTo: null,
            IsActive: null,
            Priority: null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Rule Name");
        result.DomainName.Should().Be(domain.DomainName);
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_WhenRuleDoesNotExist()
    {
        var command = new UpdateInboundRuleCommand(
            _tenantId,
            Guid.NewGuid(),
            Name: "Does Not Matter",
            MatchPattern: null,
            Action: null,
            WebhookUrl: null,
            ForwardTo: null,
            IsActive: null,
            Priority: null);

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*not found*");
    }

    private SendingDomain SeedDomain()
    {
        var domain = new SendingDomain
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DomainName = "verified.com",
            Status = DomainStatus.Verified,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Domains.Add(domain);
        _dbContext.SaveChanges();
        return domain;
    }

    private InboundRule SeedRule(Guid domainId)
    {
        var rule = new InboundRule
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DomainId = domainId,
            Name = "Original Rule",
            MatchPattern = "*@",
            Action = InboundRuleAction.Store,
            IsActive = true,
            Priority = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.InboundRules.Add(rule);
        _dbContext.SaveChanges();
        return rule;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
