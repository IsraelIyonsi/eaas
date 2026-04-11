using EaaS.Domain.Entities;
using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Api.Features.Templates;
using EaaS.Api.Tests.Helpers;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Templates;

public sealed class TemplateVersioningTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ITemplateCache _templateCache;
    private readonly Guid _tenantId = Guid.NewGuid();

    public TemplateVersioningTests()
    {
        _dbContext = DbContextFactory.Create();
        _templateCache = Substitute.For<ITemplateCache>();
    }

    [Fact]
    public async Task Should_CreateVersionSnapshot_OnUpdate()
    {
        // Arrange
        var template = CreateTestTemplate();
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateTemplateHandler(_dbContext, _templateCache);
        var command = new UpdateTemplateCommand(
            _tenantId, template.Id,
            Name: "Updated Name",
            SubjectTemplate: "Updated Subject",
            HtmlBody: "<p>Updated</p>",
            TextBody: "Updated");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        var versions = await _dbContext.TemplateVersions
            .Where(v => v.TemplateId == template.Id)
            .ToListAsync();

        versions.Should().HaveCount(1);
        versions[0].Version.Should().Be(1);
        versions[0].Name.Should().Be("Original Template");
        versions[0].Subject.Should().Be("Original Subject");
        versions[0].HtmlBody.Should().Be("<p>Original</p>");
        versions[0].TextBody.Should().Be("Original text");

        result.Version.Should().Be(2);
        result.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Should_ReturnVersionHistory_ForTemplate()
    {
        // Arrange
        var template = CreateTestTemplate();
        _dbContext.Templates.Add(template);

        var now = DateTime.UtcNow;
        _dbContext.TemplateVersions.AddRange(
            new TemplateVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Version = 1,
                Name = "V1 Name",
                Subject = "V1 Subject",
                HtmlBody = "<p>V1</p>",
                TextBody = "V1",
                CreatedAt = now.AddHours(-2)
            },
            new TemplateVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Version = 2,
                Name = "V2 Name",
                Subject = "V2 Subject",
                HtmlBody = "<p>V2</p>",
                TextBody = "V2",
                CreatedAt = now.AddHours(-1)
            });
        await _dbContext.SaveChangesAsync();

        var handler = new ListTemplateVersionsHandler(_dbContext);
        var query = new ListTemplateVersionsQuery(_tenantId, template.Id, Page: 1, PageSize: 20);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items[0].Version.Should().Be(2); // Ordered DESC
        result.Items[1].Version.Should().Be(1);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_RollbackToPreviousVersion()
    {
        // Arrange
        var template = CreateTestTemplate();
        template.Name = "Current Name";
        template.SubjectTemplate = "Current Subject";
        template.HtmlBody = "<p>Current</p>";
        template.TextBody = "Current";
        template.Version = 3;
        _dbContext.Templates.Add(template);

        _dbContext.TemplateVersions.Add(new TemplateVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            Version = 1,
            Name = "V1 Name",
            Subject = "V1 Subject",
            HtmlBody = "<p>V1</p>",
            TextBody = "V1 text",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });
        await _dbContext.SaveChangesAsync();

        var handler = new RollbackTemplateHandler(_dbContext, _templateCache);
        var command = new RollbackTemplateCommand(_tenantId, template.Id, TargetVersion: 1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("V1 Name");
        result.SubjectTemplate.Should().Be("V1 Subject");
        result.HtmlBody.Should().Be("<p>V1</p>");
        result.TextBody.Should().Be("V1 text");
    }

    [Fact]
    public async Task Should_IncrementVersionNumber_OnRollback()
    {
        // Arrange
        var template = CreateTestTemplate();
        template.Version = 3;
        _dbContext.Templates.Add(template);

        _dbContext.TemplateVersions.Add(new TemplateVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            Version = 1,
            Name = "V1 Name",
            Subject = "V1 Subject",
            HtmlBody = "<p>V1</p>",
            TextBody = "V1 text",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });
        await _dbContext.SaveChangesAsync();

        var handler = new RollbackTemplateHandler(_dbContext, _templateCache);
        var command = new RollbackTemplateCommand(_tenantId, template.Id, TargetVersion: 1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Version.Should().Be(4); // Was 3, rollback increments to 4

        // Should also create a version snapshot of the pre-rollback state
        var versions = await _dbContext.TemplateVersions
            .Where(v => v.TemplateId == template.Id)
            .OrderByDescending(v => v.Version)
            .ToListAsync();

        versions.Should().Contain(v => v.Version == 3); // snapshot of pre-rollback
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenVersionDoesNotExist()
    {
        // Arrange
        var template = CreateTestTemplate();
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        var handler = new RollbackTemplateHandler(_dbContext, _templateCache);
        var command = new RollbackTemplateCommand(_tenantId, template.Id, TargetVersion: 99);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*version*99*not found*");
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenTemplateDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        var rollbackHandler = new RollbackTemplateHandler(_dbContext, _templateCache);
        var rollbackCommand = new RollbackTemplateCommand(_tenantId, nonExistentId, TargetVersion: 1);

        var listHandler = new ListTemplateVersionsHandler(_dbContext);
        var listQuery = new ListTemplateVersionsQuery(_tenantId, nonExistentId, Page: 1, PageSize: 20);

        // Act & Assert - Rollback
        var rollbackAct = () => rollbackHandler.Handle(rollbackCommand, CancellationToken.None);
        await rollbackAct.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Template*not found*");

        // Act & Assert - List versions
        var listAct = () => listHandler.Handle(listQuery, CancellationToken.None);
        await listAct.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Template*not found*");
    }

    [Fact]
    public async Task Should_ReturnEmptyHistory_ForNewTemplate()
    {
        // Arrange
        var template = CreateTestTemplate();
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        var handler = new ListTemplateVersionsHandler(_dbContext);
        var query = new ListTemplateVersionsQuery(_tenantId, template.Id, Page: 1, PageSize: 20);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    private Template CreateTestTemplate() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        Name = "Original Template",
        SubjectTemplate = "Original Subject",
        HtmlBody = "<p>Original</p>",
        TextBody = "Original text",
        Version = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
