namespace EaaS.Domain.Interfaces;

/// <summary>
/// Per-token DKIM verification status returned from the email provider.
/// </summary>
public record DkimTokenStatus(string Token, bool IsVerified);
