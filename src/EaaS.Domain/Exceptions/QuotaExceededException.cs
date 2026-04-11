namespace EaaS.Domain.Exceptions;

public sealed class QuotaExceededException : DomainException
{
    public override int StatusCode => 429;
    public override string ErrorCode => "QUOTA_EXCEEDED";

    public QuotaExceededException(string message) : base(message) { }
}
