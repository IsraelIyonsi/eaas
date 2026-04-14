using EaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.EmailProviders.Providers.Smtp;

/// <summary>
/// Local-dev stub domain-identity service. Treats every domain as auto-verified so
/// Mailpit / docker-compose environments don't require real DKIM tokens.
/// </summary>
public sealed partial class SmtpDomainIdentityService : IDomainIdentityService
{
    private readonly ILogger<SmtpDomainIdentityService> _logger;

    public SmtpDomainIdentityService(ILogger<SmtpDomainIdentityService> logger)
    {
        _logger = logger;
    }

    public Task<DomainIdentityResult> CreateDomainIdentityAsync(string domain, CancellationToken cancellationToken = default)
    {
        LogDomainIdentityCreated(_logger, domain);
        var tokens = new List<DkimToken>
        {
            new($"local-dkim-token-1-{domain.Replace(".", "-")}"),
            new($"local-dkim-token-2-{domain.Replace(".", "-")}"),
            new($"local-dkim-token-3-{domain.Replace(".", "-")}")
        };
        return Task.FromResult(new DomainIdentityResult(true, null, tokens, null));
    }

    public Task<DomainVerificationResult> GetDomainVerificationStatusAsync(string domain, CancellationToken cancellationToken = default)
    {
        LogDomainVerificationChecked(_logger, domain);
        var statuses = new List<DkimTokenStatus>
        {
            new($"local-dkim-token-1-{domain.Replace(".", "-")}", true),
            new($"local-dkim-token-2-{domain.Replace(".", "-")}", true),
            new($"local-dkim-token-3-{domain.Replace(".", "-")}", true)
        };
        return Task.FromResult(new DomainVerificationResult(true, true, statuses, null));
    }

    public Task DeleteDomainIdentityAsync(string domain, CancellationToken cancellationToken = default)
    {
        LogDomainIdentityDeleted(_logger, domain);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMTP: domain identity created for {Domain} (local dev — auto-verified)")]
    private static partial void LogDomainIdentityCreated(ILogger logger, string domain);

    [LoggerMessage(Level = LogLevel.Information, Message = "SMTP: domain verification checked for {Domain} (local dev — always verified)")]
    private static partial void LogDomainVerificationChecked(ILogger logger, string domain);

    [LoggerMessage(Level = LogLevel.Information, Message = "SMTP: domain identity deleted for {Domain} (local dev — no-op)")]
    private static partial void LogDomainIdentityDeleted(ILogger logger, string domain);
}
