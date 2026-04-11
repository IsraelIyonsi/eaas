using EaaS.Domain.Exceptions;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.Domains;

public sealed partial class AddDomainHandler : IRequestHandler<AddDomainCommand, AddDomainResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IDomainIdentityService _emailDeliveryService;
    private readonly ISubscriptionLimitService _subscriptionLimitService;
    private readonly ILogger<AddDomainHandler> _logger;

    public AddDomainHandler(
        AppDbContext dbContext,
        IDomainIdentityService emailDeliveryService,
        ISubscriptionLimitService subscriptionLimitService,
        ILogger<AddDomainHandler> logger)
    {
        _dbContext = dbContext;
        _emailDeliveryService = emailDeliveryService;
        _subscriptionLimitService = subscriptionLimitService;
        _logger = logger;
    }

    public async Task<AddDomainResult> Handle(AddDomainCommand request, CancellationToken cancellationToken)
    {
        // Check subscription domain limit
        var canAdd = await _subscriptionLimitService.CanAddDomainAsync(request.TenantId, cancellationToken);
        if (!canAdd)
            throw new QuotaExceededException("Maximum domains reached. Upgrade your plan.");

        // Check for duplicates
        var exists = await _dbContext.Domains
            .AnyAsync(d => d.TenantId == request.TenantId && d.DomainName == request.DomainName, cancellationToken);

        if (exists)
            throw new ConflictException($"Domain '{request.DomainName}' already exists for this tenant.");

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
            RecordType = DnsRecordType.Txt,
            RecordName = request.DomainName,
            RecordValue = "v=spf1 include:amazonses.com ~all",
            Purpose = DnsRecordPurpose.Spf,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // DKIM CNAME records
        foreach (var token in sesResult.DkimTokens)
        {
            dnsRecords.Add(new DnsRecord
            {
                Id = Guid.NewGuid(),
                DomainId = domain.Id,
                RecordType = DnsRecordType.Cname,
                RecordName = $"{token.Token}._domainkey.{request.DomainName}",
                RecordValue = $"{token.Token}.dkim.amazonses.com",
                Purpose = DnsRecordPurpose.Dkim,
                IsVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // DMARC record
        dnsRecords.Add(new DnsRecord
        {
            Id = Guid.NewGuid(),
            DomainId = domain.Id,
            RecordType = DnsRecordType.Txt,
            RecordName = $"_dmarc.{request.DomainName}",
            RecordValue = "v=DMARC1; p=quarantine; rua=mailto:dmarc@israeliyonsi.dev",
            Purpose = DnsRecordPurpose.Dmarc,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        domain.DnsRecords = dnsRecords;

        _dbContext.Domains.Add(domain);
        await _dbContext.SaveChangesAsync(cancellationToken);

        LogDomainAdded(_logger, domain.Id, request.DomainName, request.TenantId);

        return new AddDomainResult(
            domain.Id,
            domain.DomainName,
            domain.Status.ToString(),
            dnsRecords.Select(r => new DnsRecordDto(
                r.Id, r.RecordType.ToString().ToUpperInvariant(), r.RecordName, r.RecordValue,
                r.Purpose.ToString().ToLowerInvariant(), r.IsVerified)).ToList(),
            domain.CreatedAt);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Domain added: DomainId={DomainId}, DomainName={DomainName}, TenantId={TenantId}")]
    private static partial void LogDomainAdded(ILogger logger, Guid domainId, string domainName, Guid tenantId);
}
