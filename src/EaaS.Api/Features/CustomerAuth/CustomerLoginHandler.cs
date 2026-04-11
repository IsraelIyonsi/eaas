using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.CustomerAuth;

public sealed partial class CustomerLoginHandler : IRequestHandler<CustomerLoginCommand, CustomerLoginResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CustomerLoginHandler> _logger;

    public CustomerLoginHandler(AppDbContext dbContext, ILogger<CustomerLoginHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CustomerLoginResult> Handle(CustomerLoginCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => EF.Functions.ILike(t.ContactEmail!, request.Email), cancellationToken);

        if (tenant is null)
        {
            LogLoginFailed(_logger, request.Email);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (string.IsNullOrEmpty(tenant.PasswordHash))
        {
            LogLoginFailed(_logger, request.Email);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, tenant.PasswordHash))
        {
            LogLoginFailed(_logger, request.Email);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (tenant.Status != Domain.Enums.TenantStatus.Active)
        {
            LogInactiveTenant(_logger, tenant.Id);
            throw new UnauthorizedAccessException("Account is suspended.");
        }

        tenant.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        LogLoginSuccess(_logger, tenant.Id, request.Email);

        return new CustomerLoginResult(
            tenant.Id,
            tenant.Name,
            tenant.ContactEmail!);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Customer login failed for {Email}")]
    private static partial void LogLoginFailed(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Suspended tenant {TenantId} attempted login")]
    private static partial void LogInactiveTenant(ILogger logger, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Customer login successful: TenantId={TenantId}, Email={Email}")]
    private static partial void LogLoginSuccess(ILogger logger, Guid tenantId, string email);
}
