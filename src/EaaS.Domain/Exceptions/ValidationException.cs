namespace EaaS.Domain.Exceptions;

public class ValidationException : DomainException
{
    public override int StatusCode => 400;
    public override string ErrorCode => "VALIDATION_ERROR";

    public ValidationException(string message) : base(message) { }
}
