namespace EaaS.Domain.Exceptions;

public abstract class DomainException : Exception
{
    public abstract int StatusCode { get; }
    public abstract string ErrorCode { get; }

    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception innerException) : base(message, innerException) { }
}
