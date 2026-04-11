using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Auth;

public sealed partial class AdminLoginHandler : IRequestHandler<AdminLoginCommand, AdminLoginResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AdminLoginHandler> _logger;

    public AdminLoginHandler(AppDbContext dbContext, ILogger<AdminLoginHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AdminLoginResult> Handle(AdminLoginCommand request, CancellationToken cancellationToken)
    {
        var adminUser = await _dbContext.AdminUsers
            .FirstOrDefaultAsync(u => EF.Functions.ILike(u.Email, request.Email), cancellationToken);

        if (adminUser is null)
        {
            LogLoginFailed(_logger, request.Email, request.IpAddress);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, adminUser.PasswordHash))
        {
            LogLoginFailed(_logger, request.Email, request.IpAddress);

            // Create audit log for failed login
            _dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                AdminUserId = adminUser.Id,
                Action = AuditAction.AdminLoginFailed,
                IpAddress = request.IpAddress,
                Details = "{}",
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!adminUser.IsActive)
        {
            LogInactiveLogin(_logger, adminUser.Id, request.IpAddress);
            throw new UnauthorizedAccessException("Account is deactivated.");
        }

        // Update last login and create audit log
        adminUser.LastLoginAt = DateTime.UtcNow;
        adminUser.UpdatedAt = DateTime.UtcNow;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUser.Id,
            Action = AuditAction.AdminLogin,
            IpAddress = request.IpAddress,
            Details = "{}",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        LogLoginSuccess(_logger, adminUser.Id, request.IpAddress);

        return new AdminLoginResult(
            adminUser.Id,
            adminUser.Email,
            adminUser.DisplayName,
            adminUser.Role.ToString());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin login failed for {Email} from {IpAddress}")]
    private static partial void LogLoginFailed(ILogger logger, string email, string ipAddress);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inactive admin user {UserId} attempted login from {IpAddress}")]
    private static partial void LogInactiveLogin(ILogger logger, Guid userId, string ipAddress);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin user {UserId} logged in from {IpAddress}")]
    private static partial void LogLoginSuccess(ILogger logger, Guid userId, string ipAddress);
}
