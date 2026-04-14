using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.PasswordReset;

public sealed partial class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordResetTokenStore _tokenStore;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        AppDbContext dbContext,
        IPasswordResetTokenStore tokenStore,
        ILogger<ResetPasswordHandler> logger)
    {
        _dbContext = dbContext;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<ResetPasswordResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = PasswordResetTokenService.HashTokenForStorage(request.Token);

        // Atomic consume: returns payload and deletes the key. Prevents double-use races.
        var payload = await _tokenStore.ConsumeTokenAsync(tokenHash, cancellationToken);
        if (payload is null)
        {
            LogInvalidToken(_logger);
            throw new UnauthorizedAccessException("This reset link is invalid or has expired.");
        }

        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == payload.TenantId, cancellationToken);

        if (tenant is null)
        {
            LogTenantMissing(_logger, payload.TenantId);
            throw new UnauthorizedAccessException("This reset link is invalid or has expired.");
        }

        tenant.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        tenant.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        LogPasswordReset(_logger, tenant.Id);
        return new ResetPasswordResult(true);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password reset attempted with invalid or expired token")]
    private static partial void LogInvalidToken(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password reset token valid but tenant {TenantId} not found")]
    private static partial void LogTenantMissing(ILogger logger, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset completed for TenantId={TenantId}")]
    private static partial void LogPasswordReset(ILogger logger, Guid tenantId);
}
