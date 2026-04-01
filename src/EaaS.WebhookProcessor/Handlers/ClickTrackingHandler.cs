using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EaaS.WebhookProcessor.Handlers;

public sealed class ClickTrackingHandler
{
    private readonly ITrackingTokenService _tokenService;
    private readonly AppDbContext _dbContext;

    public ClickTrackingHandler(ITrackingTokenService tokenService, AppDbContext dbContext)
    {
        _tokenService = tokenService;
        _dbContext = dbContext;
    }

    public async Task<IResult> HandleAsync(string token, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var trackingLink = await _dbContext.TrackingLinks
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

        if (trackingLink is null)
        {
            var data = _tokenService.ValidateToken(token);
            if (data is not null && data.EventType == "click" && !string.IsNullOrWhiteSpace(data.OriginalUrl))
                return Results.Redirect(data.OriginalUrl);

            return Results.NotFound();
        }

        try
        {
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (trackingLink.ClickedAt is null)
                trackingLink.ClickedAt = DateTime.UtcNow;

            await _dbContext.Emails
                .Where(e => e.Id == trackingLink.EmailId && e.ClickedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.ClickedAt, DateTime.UtcNow), cancellationToken);

            _dbContext.EmailEvents.Add(new EmailEvent
            {
                Id = Guid.NewGuid(),
                EmailId = trackingLink.EmailId,
                EventType = EventType.Clicked,
                Data = JsonSerializer.Serialize(new
                {
                    originalUrl = trackingLink.OriginalUrl,
                    userAgent,
                    ip
                }),
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Tracking failures should not break the redirect
        }

        return Results.Redirect(trackingLink.OriginalUrl);
    }
}
