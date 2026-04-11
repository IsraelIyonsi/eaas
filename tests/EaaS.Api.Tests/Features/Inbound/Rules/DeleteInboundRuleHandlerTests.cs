using EaaS.Api.Features.Inbound.Rules;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EaaS.Api.Tests.Features.Inbound.Rules;

public sealed class DeleteInboundRuleHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly DeleteInboundRuleHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public DeleteInboundRuleHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new DeleteInboundRuleHandler(_dbContext);
    }

    [Fact]
    public async Task Should_DeleteRule_Successfully()
    {
        var rule = SeedRule();

        var command = new DeleteInboundRuleCommand(_tenantId, rule.Id);

        await _sut.Handle(command, CancellationToken.None);

        var exists = await _dbContext.InboundRules.AnyAsync(r => r.Id == rule.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_WhenRuleDoesNotExist()
    {
        var command = new DeleteInboundRuleCommand(_tenantId, Guid.NewGuid());

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*not found*");
    }

    private InboundRule SeedRule()
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

        var rule = new InboundRule
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DomainId = domain.Id,
            Name = "Rule To Delete",
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
