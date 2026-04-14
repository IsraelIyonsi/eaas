namespace EaaS.Domain.Providers;

/// <summary>
/// Provider-agnostic representation of an email attachment. The <see cref="Content"/>
/// stream is owned by the caller — providers must not dispose it.
/// </summary>
public sealed record EmailAttachment(
    string Filename,
    string ContentType,
    Stream Content,
    string? ContentId = null);
