using EaaS.Domain.Exceptions;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Domains;

public sealed class VerifyDomainHandler : IRequestHandler<VerifyDomainCommand, VerifyDomainResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IDomainIdentityService _emailDeliveryService;

    public VerifyDomainHandler(AppDbContext dbContext, IDomainIdentityService emailDeliveryService)
    {
        _dbContext = dbContext;
        _emailDeliveryService = emailDeliveryService;
    }

    public async Task<VerifyDomainResult> Handle(VerifyDomainCommand request, CancellationToken cancellationToken)
    {
        var domain = await _dbContext.Domains
            .Include(d => d.DnsRecords)
            .Where(d => d.Id == request.Id && d.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Domain with id '{request.Id}' not found.");

        // Check SES verification status
        var sesResult = await _emailDeliveryService.GetDomainVerificationStatusAsync(domain.DomainName, cancellationToken);

        if (sesResult.Success)
        {
            // Update DKIM record statuses
            foreach (var dkimStatus in sesResult.DkimStatuses)
            {
                var matchingRecord = domain.DnsRecords
                    .FirstOrDefault(r => r.Purpose == DnsRecordPurpose.Dkim &&
                                         r.RecordName.StartsWith(dkimStatus.Token, StringComparison.Ordinal));

                if (matchingRecord is not null)
                {
                    matchingRecord.IsVerified = dkimStatus.IsVerified;
                    if (dkimStatus.IsVerified)
                        matchingRecord.VerifiedAt = DateTime.UtcNow;
                    matchingRecord.UpdatedAt = DateTime.UtcNow;
                }
            }

            // If SES says domain is verified, mark SPF and DMARC as verified too
            if (sesResult.IsVerified)
            {
                foreach (var record in domain.DnsRecords)
                {
                    record.IsVerified = true;
                    record.VerifiedAt ??= DateTime.UtcNow;
                    record.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Update domain status
            var allVerified = domain.DnsRecords.All(r => r.IsVerified);
            domain.Status = allVerified ? DomainStatus.Verified : DomainStatus.PendingVerification;
            domain.LastCheckedAt = DateTime.UtcNow;

            if (allVerified)
                domain.VerifiedAt ??= DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new VerifyDomainResult(
            domain.Id,
            domain.DomainName,
            domain.Status.ToString(),
            domain.DnsRecords.Select(r => new DnsRecordDto(
                r.Id, r.RecordType.ToString().ToUpperInvariant(), r.RecordName, r.RecordValue,
                r.Purpose.ToString().ToLowerInvariant(), r.IsVerified)).ToList(),
            domain.VerifiedAt);
    }
}
