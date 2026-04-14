using EaaS.Api.Features.Emails;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Emails;

// BUG-M3: GET /emails/{id} accepts either the internal GUID or the public `snx_` MessageId.
public sealed class GetEmailHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly GetEmailHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetEmailHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new GetEmailHandler(_dbContext);
    }

    private Email SeedEmail(string messageId, Guid? tenantId = null)
    {
        var email = new Email
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? _tenantId,
            ApiKeyId = Guid.NewGuid(),
            MessageId = messageId,
            FromEmail = "sender@verified.com",
            ToEmails = "[\"recipient@example.com\"]",
            CcEmails = "[]",
            BccEmails = "[]",
            Subject = "Hi",
            HtmlBody = "<p>Hi</p>",
            TextBody = "Hi",
            Status = EmailStatus.Queued,
            Metadata = "{}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Emails.Add(email);
        _dbContext.SaveChanges();
        return email;
    }

    [Fact]
    public async Task Should_LookupByGuid_When_IdentifierIsGuid()
    {
        var email = SeedEmail("snx_abc123");

        var result = await _sut.Handle(
            new GetEmailQuery(_tenantId, email.Id.ToString()),
            CancellationToken.None);

        result.Id.Should().Be(email.Id);
        result.MessageId.Should().Be("snx_abc123");
    }

    [Fact]
    public async Task Should_LookupByMessageId_When_IdentifierStartsWithSnxPrefix()
    {
        var email = SeedEmail("snx_public_xyz");

        var result = await _sut.Handle(
            new GetEmailQuery(_tenantId, "snx_public_xyz"),
            CancellationToken.None);

        result.Id.Should().Be(email.Id);
        result.MessageId.Should().Be("snx_public_xyz");
    }

    [Fact]
    public async Task Should_Throw404_When_IdentifierIsNeitherGuidNorSnxPrefix()
    {
        var act = () => _sut.Handle(
            new GetEmailQuery(_tenantId, "garbage-id"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_Throw404_When_MessageIdBelongsToDifferentTenant()
    {
        var otherTenant = Guid.NewGuid();
        SeedEmail("snx_tenant_isolated", tenantId: otherTenant);

        var act = () => _sut.Handle(
            new GetEmailQuery(_tenantId, "snx_tenant_isolated"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
