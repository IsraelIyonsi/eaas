namespace EaaS.Api.Features.Admin.Health;

public sealed record SystemHealthResult(
    string Status,
    DatabaseHealthResult Database,
    RedisHealthResult Redis,
    int TenantCount,
    int EmailCount);

public sealed record DatabaseHealthResult(string Status, string? Error);

public sealed record RedisHealthResult(string Status, string? Error);
