using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using EaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.EmailProviders.Providers.Ses;

/// <summary>
/// SES-backed implementation of <see cref="IDomainIdentityService"/>. Split out from
/// the legacy <c>SesEmailService</c> for Interface Segregation — a provider can ship
/// outbound send without ever implementing domain identity.
/// </summary>
public sealed partial class SesDomainIdentityService : IDomainIdentityService
{
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private readonly ILogger<SesDomainIdentityService> _logger;

    public SesDomainIdentityService(
        IAmazonSimpleEmailServiceV2 sesClient,
        ILogger<SesDomainIdentityService> logger)
    {
        _sesClient = sesClient;
        _logger = logger;
    }

    public async Task<DomainIdentityResult> CreateDomainIdentityAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CreateEmailIdentityRequest
            {
                EmailIdentity = domain,
                DkimSigningAttributes = new DkimSigningAttributes
                {
                    NextSigningKeyLength = DkimSigningKeyLength.RSA_2048_BIT
                }
            };

            var response = await _sesClient.CreateEmailIdentityAsync(request, cancellationToken);

            var dkimTokens = response.DkimAttributes?.Tokens?
                .Select(t => new DkimToken(t))
                .ToList() ?? new List<DkimToken>();

            LogDomainIdentityCreated(_logger, domain, dkimTokens.Count);

            return new DomainIdentityResult(true, null, dkimTokens, null);
        }
        catch (AlreadyExistsException)
        {
            LogDomainAlreadyExists(_logger, domain);
            return new DomainIdentityResult(false, null, Array.Empty<DkimToken>(), $"Domain '{domain}' already exists in SES.");
        }
        catch (Exception ex)
        {
            LogDomainIdentityFailed(_logger, ex, domain);
            return new DomainIdentityResult(false, null, Array.Empty<DkimToken>(), ex.Message);
        }
    }

    public async Task<DomainVerificationResult> GetDomainVerificationStatusAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetEmailIdentityRequest { EmailIdentity = domain };
            var response = await _sesClient.GetEmailIdentityAsync(request, cancellationToken);

            var isVerified = response.VerifiedForSendingStatus;

            var dkimStatuses = response.DkimAttributes?.Tokens?
                .Select(t => new DkimTokenStatus(t, response.DkimAttributes.Status == DkimStatus.SUCCESS))
                .ToList() ?? new List<DkimTokenStatus>();

            LogDomainVerificationChecked(_logger, domain, isVerified);

            return new DomainVerificationResult(true, isVerified, dkimStatuses, null);
        }
        catch (NotFoundException)
        {
            return new DomainVerificationResult(false, false, Array.Empty<DkimTokenStatus>(), $"Domain '{domain}' not found in SES.");
        }
        catch (Exception ex)
        {
            LogDomainVerificationFailed(_logger, ex, domain);
            return new DomainVerificationResult(false, false, Array.Empty<DkimTokenStatus>(), ex.Message);
        }
    }

    public async Task DeleteDomainIdentityAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sesClient.DeleteEmailIdentityAsync(
                new DeleteEmailIdentityRequest { EmailIdentity = domain },
                cancellationToken);
            LogDomainIdentityDeleted(_logger, domain);
        }
        catch (Exception ex)
        {
            LogDomainIdentityDeleteFailed(_logger, ex, domain);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SES domain identity created for {Domain} with {TokenCount} DKIM tokens")]
    private static partial void LogDomainIdentityCreated(ILogger logger, string domain, int tokenCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Domain {Domain} already exists in SES")]
    private static partial void LogDomainAlreadyExists(ILogger logger, string domain);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create SES domain identity for {Domain}")]
    private static partial void LogDomainIdentityFailed(ILogger logger, Exception ex, string domain);

    [LoggerMessage(Level = LogLevel.Information, Message = "Domain verification checked for {Domain}, verified: {IsVerified}")]
    private static partial void LogDomainVerificationChecked(ILogger logger, string domain, bool isVerified);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to check domain verification for {Domain}")]
    private static partial void LogDomainVerificationFailed(ILogger logger, Exception ex, string domain);

    [LoggerMessage(Level = LogLevel.Information, Message = "SES domain identity deleted for {Domain}")]
    private static partial void LogDomainIdentityDeleted(ILogger logger, string domain);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete SES domain identity for {Domain}")]
    private static partial void LogDomainIdentityDeleteFailed(ILogger logger, Exception ex, string domain);
}
