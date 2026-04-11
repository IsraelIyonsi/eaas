using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace EaaS.Api.Features.Admin.Health;

public sealed class GetSystemHealthHandler : IRequestHandler<GetSystemHealthQuery, SystemHealthResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;

    public GetSystemHealthHandler(AppDbContext dbContext, IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _redis = redis;
    }

    public async Task<SystemHealthResult> Handle(GetSystemHealthQuery request, CancellationToken cancellationToken)
    {
        DatabaseHealthResult dbHealth;
        int tenantCount = 0;
        int emailCount = 0;

        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                tenantCount = await _dbContext.Tenants.CountAsync(cancellationToken);
                emailCount = await _dbContext.Emails.CountAsync(cancellationToken);
                dbHealth = new DatabaseHealthResult("healthy", null);
            }
            else
            {
                dbHealth = new DatabaseHealthResult("unhealthy", "Cannot connect to database");
            }
        }
        catch (Exception ex)
        {
            dbHealth = new DatabaseHealthResult("unhealthy", ex.Message);
        }

        RedisHealthResult redisHealth;
        try
        {
            redisHealth = _redis.IsConnected
                ? new RedisHealthResult("healthy", null)
                : new RedisHealthResult("unhealthy", "Redis is not connected");
        }
        catch (Exception ex)
        {
            redisHealth = new RedisHealthResult("unhealthy", ex.Message);
        }

        var overallStatus = dbHealth.Status == "healthy" && redisHealth.Status == "healthy"
            ? "healthy"
            : "degraded";

        return new SystemHealthResult(overallStatus, dbHealth, redisHealth, tenantCount, emailCount);
    }
}
