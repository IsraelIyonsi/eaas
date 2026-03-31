using EaaS.Api.Features.Templates;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Templates;

public sealed class CreateTemplateHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CreateTemplateHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateTemplateHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new CreateTemplateHandler(_dbContext);
    }

    [Fact]
    public async Task Should_CreateTemplate_When_Valid()
    {
        var command = TestDataBuilders.CreateTemplate()
            .WithTenantId(_tenantId)
            .WithName("Order Confirmation")
            .Build();

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("Order Confirmation");
        result.Version.Should().Be(1);

        var stored = await _dbContext.Templates.FindAsync(result.Id);
        stored.Should().NotBeNull();
        stored!.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Should_ThrowConflict_When_DuplicateName()
    {
        _dbContext.Templates.Add(new Template
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Duplicate Template",
            SubjectTemplate = "Subject",
            HtmlBody = "<p>Body</p>",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var command = TestDataBuilders.CreateTemplate()
            .WithTenantId(_tenantId)
            .WithName("Duplicate Template")
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<EaaS.Domain.Exceptions.ConflictException>()
            .WithMessage("*already exists*");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
