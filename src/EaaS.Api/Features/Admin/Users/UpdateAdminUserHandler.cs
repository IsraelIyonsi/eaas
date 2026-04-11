using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Users;

public sealed class UpdateAdminUserHandler : IRequestHandler<UpdateAdminUserCommand, AdminUserResult>
{
    private readonly AppDbContext _dbContext;

    public UpdateAdminUserHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminUserResult> Handle(UpdateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (user is null)
            throw new NotFoundException("Admin user not found");

        var now = DateTime.UtcNow;

        if (request.Email is not null) user.Email = request.Email;
        if (request.DisplayName is not null) user.DisplayName = request.DisplayName;
        if (request.Password is not null) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        if (request.Role is not null)
        {
            if (!Enum.TryParse<AdminRole>(request.Role, ignoreCase: true, out var role))
                throw new ValidationException($"Invalid role '{request.Role}'. Must be one of: {string.Join(", ", Enum.GetNames<AdminRole>())}");
            user.Role = role;
        }

        user.UpdatedAt = now;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = request.AdminUserId,
            Action = AuditAction.AdminUserUpdated,
            TargetType = "AdminUser",
            TargetId = user.Id.ToString(),
            Details = JsonSerializer.Serialize(new { email = user.Email }),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

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
