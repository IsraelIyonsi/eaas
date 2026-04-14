namespace EaaS.Domain.Interfaces;

/// <summary>
/// Sends password reset emails. Split from the implementation so it can be faked in tests.
/// </summary>
public interface IPasswordResetEmailSender
{
    Task SendResetEmailAsync(string recipientEmail, string token, CancellationToken cancellationToken = default);
}
