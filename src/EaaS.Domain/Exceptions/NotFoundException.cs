namespace EaaS.Domain.Exceptions;

public class NotFoundException : DomainException
{
    public override int StatusCode => 404;
    public override string ErrorCode => "NOT_FOUND";

    public NotFoundException(string message) : base(message) { }
}
