using EaaS.Api.Features.Templates;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace EaaS.Api.Tests.Features.Templates;

/// <summary>
/// Regression tests for the production bug where DELETE /api/v1/templates/{id}
/// returned 502 Bad Gateway. Root cause: the handler propagated infrastructure
/// faults (Redis cache invalidation, DbUpdateException) out of the request
/// pipeline. The cache call *is* try/caught inside RedisCacheService, but any
/// exception thrown from EF (concurrency, connection drop, unique-index
/// violation during soft-delete reactivation) bubbled up as a 500 — which,
/// combined with response stream aborts, presented to the BFF as a 502 because
/// the fetch() promise rejected before the upstream finished writing headers.
///
/// The fix wraps the DB write + cache invalidation in a well-scoped try/catch
/// that maps infrastructure faults to a clean domain exception AND guarantees
/// the response body is written before the handler returns.
/// </summary>
public sealed class DeleteTemplateHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ITemplateCache _templateCache;
    private readonly DeleteTemplateHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public DeleteTemplateHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _templateCache = Substitute.For<ITemplateCache>();
        _sut = new DeleteTemplateHandler(_dbContext, _templateCache, NullLogger<DeleteTemplateHandler>.Instance);
    }

    [Fact]
    public async Task Should_SoftDeleteTemplate_When_Exists()
    {
        var template = CreateTestTemplate();
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        var command = new DeleteTemplateCommand(_tenantId, template.Id);

        await _sut.Handle(command, CancellationToken.None);

        var reloaded = await _dbContext.Templates.FindAsync(template.Id);
        reloaded.Should().NotBeNull();
        reloaded!.DeletedAt.Should().NotBeNull();
        reloaded.DeletedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_InvalidateCache_After_SoftDelete()
    {
        var template = CreateTestTemplate();
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        var command = new DeleteTemplateCommand(_tenantId, template.Id);

        await _sut.Handle(command, CancellationToken.None);

        await _templateCache.Received(1).InvalidateTemplateCacheAsync(template.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowNotFound_When_TemplateDoesNotExist()
    {
        var command = new DeleteTemplateCommand(_tenantId, Guid.NewGuid());

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Should_ThrowNotFound_When_AlreadySoftDeleted()
    {
        var template = CreateTestTemplate();
        template.DeletedAt = DateTime.UtcNow.AddHours(-1);
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        var command = new DeleteTemplateCommand(_tenantId, template.Id);

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_IsolateByTenant_When_DeletingTemplate()
    {
        var otherTenantId = Guid.NewGuid();
        var template = CreateTestTemplate();
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        // Attempt delete using a different tenant — must not touch this template.
        var command = new DeleteTemplateCommand(otherTenantId, template.Id);
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();

        var reloaded = await _dbContext.Templates.FindAsync(template.Id);
        reloaded!.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Should_SucceedAndSwallowCacheFailure_When_RedisIsDown()
    {
        // THIS IS THE 502 REGRESSION: when Redis is unreachable the cache
        // invalidation must NOT break the DB commit nor bubble an exception
        // out of the handler. The user's DELETE must still succeed.
        var template = CreateTestTemplate();
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        _templateCache
            .InvalidateTemplateCacheAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Simulated Redis timeout"));

        var command = new DeleteTemplateCommand(_tenantId, template.Id);

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();

        var reloaded = await _dbContext.Templates.FindAsync(template.Id);
        reloaded!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_PreserveTemplateVersions_After_SoftDelete()
    {
        // Soft delete must NOT cascade — version history must be preserved so
        // audit queries (ListTemplateVersions on soft-deleted templates) keep
        // working. EF cascade on hard-delete is fine; soft-delete is an UPDATE
        // and must leave template_versions untouched.
        var template = CreateTestTemplate();
        _dbContext.Templates.Add(template);
        _dbContext.TemplateVersions.Add(new TemplateVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            Version = 1,
            Name = template.Name,
            Subject = template.SubjectTemplate,
            HtmlBody = template.HtmlBody,
            TextBody = template.TextBody,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var command = new DeleteTemplateCommand(_tenantId, template.Id);
        await _sut.Handle(command, CancellationToken.None);

        var versions = await _dbContext.TemplateVersions
            .Where(v => v.TemplateId == template.Id)
            .ToListAsync();
        versions.Should().HaveCount(1);
    }

    private Template CreateTestTemplate() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        Name = "Template to delete",
        SubjectTemplate = "Subject",
        HtmlBody = "<p>Body</p>",
        TextBody = "Body",
        Version = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
