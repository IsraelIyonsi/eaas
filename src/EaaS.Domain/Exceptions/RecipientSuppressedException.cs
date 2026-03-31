namespace EaaS.Domain.Exceptions;

public class RecipientSuppressedException : DomainException
{
    public override int StatusCode => 422;
    public override string ErrorCode => "RECIPIENT_SUPPRESSED";

    public RecipientSuppressedException(string message) : base(message) { }
}
