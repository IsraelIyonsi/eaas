using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EaaS.WebhookProcessor.Handlers;

public sealed class OpenTrackingHandler
{
    private static readonly byte[] TransparentGif =
    {
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00,
        0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00,
        0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44,
        0x01, 0x00, 0x3B
    };

    private readonly ITrackingTokenService _tokenService;
    private readonly AppDbContext _dbContext;

    public OpenTrackingHandler(ITrackingTokenService tokenService, AppDbContext dbContext)
    {
        _tokenService = tokenService;
        _dbContext = dbContext;
    }

    public async Task<IResult> HandleAsync(string token, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var data = _tokenService.ValidateToken(token);
        if (data is not null && data.EventType == "open")
        {
            try
            {
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                await _dbContext.Emails
                    .Where(e => e.Id == data.EmailId && e.OpenedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.OpenedAt, DateTime.UtcNow), cancellationToken);

                _dbContext.EmailEvents.Add(new EmailEvent
                {
                    Id = Guid.NewGuid(),
                    EmailId = data.EmailId,
                    EventType = EventType.Opened,
                    Data = JsonSerializer.Serialize(new { userAgent, ip }),
                    CreatedAt = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // Tracking failures should not break the pixel response
            }
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Results.File(TransparentGif, "image/gif");
    }
}
