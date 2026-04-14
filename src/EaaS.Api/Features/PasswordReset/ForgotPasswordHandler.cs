using System.Security.Cryptography;
using System.Text;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EaaS.Api.Features.PasswordReset;

public sealed partial class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordResetTokenStore _tokenStore;
    private readonly PasswordResetTokenService _tokenService;
    private readonly IPasswordResetEmailSender _emailSender;
    private readonly PasswordResetSettings _settings;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(
        AppDbContext dbContext,
        IPasswordResetTokenStore tokenStore,
        PasswordResetTokenService tokenService,
        IPasswordResetEmailSender emailSender,
        IOptions<PasswordResetSettings> settings,
        ILogger<ForgotPasswordHandler> logger)
    {
        _dbContext = dbContext;
        _tokenStore = tokenStore;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ForgotPasswordResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromHours(1);
        var emailHash = HashForBucket(request.Email.ToLowerInvariant());
        var emailBucket = $"email:{emailHash}";

        var emailHits = await _tokenStore.IncrementRateLimitAsync(emailBucket, window, cancellationToken);
        if (emailHits > _settings.MaxRequestsPerEmailPerHour)
        {
            LogRateLimited(_logger, "email", emailHits);
            // Silent drop — still return success to avoid enumeration.
            return new ForgotPasswordResult(true);
        }

        if (!string.IsNullOrWhiteSpace(request.ClientIp))
        {
            var ipBucket = $"ip:{request.ClientIp}";
            var ipHits = await _tokenStore.IncrementRateLimitAsync(ipBucket, window, cancellationToken);
            if (ipHits > _settings.MaxRequestsPerIpPerHour)
            {
                LogRateLimited(_logger, "ip", ipHits);
                return new ForgotPasswordResult(true);
            }
        }

        // Look up the tenant. Always return success regardless of result (no enumeration).
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.ContactEmail != null)
            .FirstOrDefaultAsync(t => EF.Functions.ILike(t.ContactEmail!, request.Email), cancellationToken);

        if (tenant is null)
        {
            LogUnknownEmail(_logger, request.Email);
            return new ForgotPasswordResult(true);
        }

        // Generate token, store hashed, send email.
        var token = _tokenService.GenerateToken(tenant.Id, tenant.ContactEmail!, DateTimeOffset.UtcNow);
        var tokenHash = PasswordResetTokenService.HashTokenForStorage(token);
        var ttl = TimeSpan.FromMinutes(_settings.TokenLifetimeMinutes);

        await _tokenStore.StoreTokenAsync(tokenHash, tenant.Id, tenant.ContactEmail!, ttl, cancellationToken);

        try
        {
            await _emailSender.SendResetEmailAsync(tenant.ContactEmail!, token, cancellationToken);
        }
        catch (Exception ex)
        {
            LogSendException(_logger, ex, tenant.ContactEmail!);
            // Swallow — still return success so callers can't probe delivery status.
        }

        LogResetRequested(_logger, tenant.Id, tenant.ContactEmail!);
        return new ForgotPasswordResult(true);
    }

    private static string HashForBucket(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset requested for TenantId={TenantId}, Email={Email}")]
    private static partial void LogResetRequested(ILogger logger, Guid tenantId, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset requested for unknown email {Email} — silently dropping")]
    private static partial void LogUnknownEmail(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password reset rate limit exceeded (bucket={Bucket}, hits={Hits})")]
    private static partial void LogRateLimited(ILogger logger, string bucket, long hits);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send password reset email to {Email}")]
    private static partial void LogSendException(ILogger logger, Exception ex, string email);
}
