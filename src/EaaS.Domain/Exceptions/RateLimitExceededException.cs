namespace EaaS.Domain.Exceptions;

public class RateLimitExceededException : DomainException
{
    public override int StatusCode => 429;
    public override string ErrorCode => "RATE_LIMIT_EXCEEDED";

    public RateLimitExceededException(string message) : base(message) { }
}
