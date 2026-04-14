namespace EaaS.Domain.Interfaces;

public interface ITrackingTokenService
{
    string GenerateToken(Guid emailId, string eventType);
    TrackingTokenData? ValidateToken(string token);
}

public sealed record TrackingTokenData(Guid EmailId, string EventType);
