using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Domains;

public sealed class AddDomainHandler : IRequestHandler<AddDomainCommand, AddDomainResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailDeliveryService _emailDeliveryService;

    public AddDomainHandler(AppDbContext dbContext, IEmailDeliveryService emailDeliveryService)
    {
        _dbContext = dbContext;
        _emailDeliveryService = emailDeliveryService;
    }

    public async Task<AddDomainResult> Handle(AddDomainCommand request, CancellationToken cancellationToken)
    {
        // Check for duplicates
        var exists = await _dbContext.Domains
            .AnyAsync(d => d.TenantId == request.TenantId && d.DomainName == request.DomainName, cancellationToken);

        if (exists)
            throw new InvalidOperationException($"Domain '{request.DomainName}' already exists for this tenant.");

        // Call SES to create the domain identity
        var sesResult = await _emailDeliveryService.CreateDomainIdentityAsync(request.DomainName, cancellationToken);

        if (!sesResult.Success)
            throw new InvalidOperationException($"Failed to create SES identity: {sesResult.ErrorMessage}");

        // Create domain entity
        var domain = new SendingDomain
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            DomainName = request.DomainName,
            Status = DomainStatus.PendingVerification,
            SesIdentityArn = sesResult.IdentityArn,
            CreatedAt = DateTime.UtcNow
        };

        // Build DNS records from SES DKIM tokens
        var dnsRecords = new List<DnsRecord>();

        // SPF record
        dnsRecords.Add(new DnsRecord
        {
            Id = Guid.NewGuid(),
            DomainId = domain.Id,
            RecordType = "TXT",
            RecordName = request.DomainName,
            RecordValue = "v=spf1 include:amazonses.com ~all",
            Purpose = DnsRecordPurpose.Spf,
            IsVerified = false,
            UpdatedAt = DateTime.UtcNow
        });

        // DKIM CNAME records
        foreach (var token in sesResult.DkimTokens)
        {
            dnsRecords.Add(new DnsRecord
            {
                Id = Guid.NewGuid(),
                DomainId = domain.Id,
                RecordType = "CNAME",
                RecordName = $"{token.Token}._domainkey.{request.DomainName}",
                RecordValue = $"{token.Token}.dkim.amazonses.com",
                Purpose = DnsRecordPurpose.Dkim,
                IsVerified = false,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // DMARC record
        dnsRecords.Add(new DnsRecord
        {
            Id = Guid.NewGuid(),
            DomainId = domain.Id,
            RecordType = "TXT",
            RecordName = $"_dmarc.{request.DomainName}",
            RecordValue = "v=DMARC1; p=quarantine; rua=mailto:dmarc@israeliyonsi.dev",
            Purpose = DnsRecordPurpose.Dmarc,
            IsVerified = false,
            UpdatedAt = DateTime.UtcNow
        });

        domain.DnsRecords = dnsRecords;

        _dbContext.Domains.Add(domain);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AddDomainResult(
            domain.Id,
            domain.DomainName,
            domain.Status.ToString(),
            dnsRecords.Select(r => new DnsRecordDto(
                r.Id, r.RecordType, r.RecordName, r.RecordValue,
                r.Purpose.ToString().ToLowerInvariant(), r.IsVerified)).ToList(),
            domain.CreatedAt);
    }
}
