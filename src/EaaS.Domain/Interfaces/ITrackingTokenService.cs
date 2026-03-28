namespace EaaS.Domain.Interfaces;

public interface ITrackingTokenService
{
    string GenerateToken(Guid emailId, string eventType, string? originalUrl = null);
    TrackingTokenData? ValidateToken(string token);
}

public sealed record TrackingTokenData(Guid EmailId, string EventType, string? OriginalUrl);
