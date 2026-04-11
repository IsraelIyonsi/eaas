using EaaS.Api.Features.Inbound.Rules;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Inbound.Rules;

public sealed class CreateInboundRuleHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CreateInboundRuleHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateInboundRuleHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new CreateInboundRuleHandler(_dbContext);
    }

    [Fact]
    public async Task Should_CreateRule_WhenValid()
    {
        var domain = SeedDomain();

        var command = TestDataBuilders.CreateInboundRule()
            .WithTenantId(_tenantId)
            .WithDomainId(domain.Id)
            .WithName("Support Catch-All")
            .WithMatchPattern("support@")
            .WithAction(InboundRuleAction.Store)
            .WithPriority(10)
            .Build();

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("Support Catch-All");
        result.MatchPattern.Should().Be("support@");
        result.DomainName.Should().Be(domain.DomainName);
        result.Priority.Should().Be(10);
        result.IsActive.Should().BeTrue();

        var stored = await _dbContext.InboundRules.FindAsync(result.Id);
        stored.Should().NotBeNull();
        stored!.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_WhenDomainDoesNotExist()
    {
        var command = TestDataBuilders.CreateInboundRule()
            .WithTenantId(_tenantId)
            .WithDomainId(Guid.NewGuid())
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Domain not found*");
    }

    [Fact]
    public async Task Should_ThrowConflictException_WhenNameAlreadyExists()
    {
        var domain = SeedDomain();

        _dbContext.InboundRules.Add(new InboundRule
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DomainId = domain.Id,
            Name = "Existing Rule",
            MatchPattern = "*@",
            Action = InboundRuleAction.Store,
            IsActive = true,
            Priority = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var command = TestDataBuilders.CreateInboundRule()
            .WithTenantId(_tenantId)
            .WithDomainId(domain.Id)
            .WithName("Existing Rule")
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
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

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
