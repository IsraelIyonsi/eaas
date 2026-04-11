using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Metrics;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Users;

public sealed class CreateAdminUserHandler : IRequestHandler<CreateAdminUserCommand, AdminUserResult>
{
    private readonly AppDbContext _dbContext;

    public CreateAdminUserHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminUserResult> Handle(CreateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var emailExists = await _dbContext.AdminUsers
            .AsNoTracking()
            .AnyAsync(u => EF.Functions.ILike(u.Email, request.Email), cancellationToken);

        if (emailExists)
            throw new ConflictException("An admin user with this email already exists");

        if (!Enum.TryParse<AdminRole>(request.Role, ignoreCase: true, out var role))
            throw new ValidationException($"Invalid role '{request.Role}'. Must be one of: {string.Join(", ", Enum.GetNames<AdminRole>())}");

        var now = DateTime.UtcNow;

        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.AdminUsers.Add(user);

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = request.AdminUserId,
            Action = AuditAction.AdminUserCreated,
            TargetType = "AdminUser",
            TargetId = user.Id.ToString(),
            Details = JsonSerializer.Serialize(new { email = user.Email }),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        EmailMetrics.AdminOperationsTotal.WithLabels("user_create").Inc();

        return new AdminUserResult(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString().ToLowerInvariant(),
            user.IsActive,
            user.LastLoginAt,
            user.CreatedAt,
            user.UpdatedAt);
    }
}
