using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Metrics;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Users;

public sealed class DeleteAdminUserHandler : IRequestHandler<DeleteAdminUserCommand>
{
    private readonly AppDbContext _dbContext;

    public DeleteAdminUserHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteAdminUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (user is null)
            throw new NotFoundException("Admin user not found");

        var now = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAt = now;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = request.AdminUserId,
            Action = AuditAction.AdminUserDeleted,
            TargetType = "AdminUser",
            TargetId = user.Id.ToString(),
            Details = JsonSerializer.Serialize(new { email = user.Email }),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        EmailMetrics.AdminOperationsTotal.WithLabels("user_delete").Inc();
    }
}
