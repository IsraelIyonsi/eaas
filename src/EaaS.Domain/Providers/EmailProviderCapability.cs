namespace EaaS.Domain.Providers;

/// <summary>
/// Declarative capability descriptor for an <see cref="IEmailProvider"/>.
/// Callers query <see cref="IEmailProvider.Capabilities"/> to branch on what a given
/// adapter supports (e.g. tagging, custom variables, raw MIME). Capability values are
/// chosen per the Phase 0 brief — the union of the architect doc's capability set and
/// the Mailgun-Flex-specific needs.
/// </summary>
[System.Flags]
public enum EmailProviderCapability
{
    None = 0,
    Attachments = 1 << 0,
    Tags = 1 << 1,
    CustomVariables = 1 << 2,
    Templates = 1 << 3,
    Batch = 1 << 4,
    Nonces = 1 << 5,
    SendRaw = 1 << 6,
    DomainIdentity = 1 << 7
}
