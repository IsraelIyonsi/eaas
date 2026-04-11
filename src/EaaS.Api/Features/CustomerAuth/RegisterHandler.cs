using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Utilities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.CustomerAuth;

public sealed partial class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<RegisterHandler> _logger;

    public RegisterHandler(AppDbContext dbContext, ILogger<RegisterHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var emailExists = await _dbContext.Tenants
            .AnyAsync(t => EF.Functions.ILike(t.ContactEmail!, request.Email), cancellationToken);

        if (emailExists)
        {
            LogDuplicateEmail(_logger, request.Email);
            throw new ConflictException($"A tenant with email '{request.Email}' already exists.");
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ContactEmail = request.Email,
            CompanyName = request.CompanyName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = TenantStatus.Active,
            MonthlyEmailLimit = 3000,
            MaxApiKeys = 3,
            MaxDomainsCount = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var plaintextKey = ApiKeyGenerator.GenerateKey();
        var keyHash = ApiKeyGenerator.ComputeSha256Hash(plaintextKey);
        var prefix = plaintextKey[..8];

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Default",
            KeyHash = keyHash,
            Prefix = prefix,
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(cancellationToken);

        LogRegistrationSuccess(_logger, tenant.Id, request.Email);

        return new RegisterResult(
            tenant.Id,
            tenant.Name,
            tenant.ContactEmail!,
            plaintextKey);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registration attempted with existing email: {Email}")]
    private static partial void LogDuplicateEmail(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Customer registered: TenantId={TenantId}, Email={Email}")]
    private static partial void LogRegistrationSuccess(ILogger logger, Guid tenantId, string email);
}
