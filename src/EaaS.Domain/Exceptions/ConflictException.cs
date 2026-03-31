namespace EaaS.Domain.Exceptions;

public class ConflictException : DomainException
{
    public override int StatusCode => 409;
    public override string ErrorCode => "CONFLICT";

    public ConflictException(string message) : base(message) { }
}
