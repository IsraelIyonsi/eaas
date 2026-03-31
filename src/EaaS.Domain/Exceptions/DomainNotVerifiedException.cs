namespace EaaS.Domain.Exceptions;

public class DomainNotVerifiedException : DomainException
{
    public override int StatusCode => 422;
    public override string ErrorCode => "DOMAIN_NOT_VERIFIED";

    public DomainNotVerifiedException(string message) : base(message) { }
}
