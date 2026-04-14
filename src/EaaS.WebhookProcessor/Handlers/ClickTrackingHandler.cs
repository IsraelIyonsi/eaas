using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.WebhookProcessor.Handlers;

public sealed partial class ClickTrackingHandler
{
    private readonly ITrackingTokenService _tokenService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ClickTrackingHandler> _logger;

    public ClickTrackingHandler(ITrackingTokenService tokenService, AppDbContext dbContext, ILogger<ClickTrackingHandler> logger)
    {
        _tokenService = tokenService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IResult> HandleAsync(string token, HttpContext httpContext, CancellationToken cancellationToken)
    {
        // Security: clicks must only redirect to URLs we embedded at send time.
        // The destination is NEVER taken from the request (query/body/HMAC payload) —
        // only from the TrackingLink row stored when the outgoing email was rewritten.
        // This eliminates open-redirect even if signing keys are ever compromised.
        if (string.IsNullOrWhiteSpace(token))
            return Results.NotFound();

        var trackingLink = await _dbContext.TrackingLinks
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

        if (trackingLink is null)
        {
            LogUnknownToken(_logger, Truncate(token));
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
        catch (Exception ex)
        {
            // Tracking failures should not break the redirect
            LogClickTrackingFailed(_logger, trackingLink.EmailId, ex);
        }

        return Results.Redirect(trackingLink.OriginalUrl);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Click tracking event failed to persist for EmailId={EmailId}")]
    private static partial void LogClickTrackingFailed(ILogger logger, Guid emailId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Click tracking token not found in store: {TokenPrefix}")]
    private static partial void LogUnknownToken(ILogger logger, string tokenPrefix);

    private static string Truncate(string value) =>
        value.Length <= 8 ? value : value[..8] + "...";
}
