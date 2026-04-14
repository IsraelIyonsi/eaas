namespace EaaS.Application.Email.Providers;

/// <summary>Static capability matrix for a provider. Drives capability-gated contract tests.</summary>
public sealed record ProviderCapabilities(
    bool SupportsAttachments,
    bool SupportsCustomVariables,
    bool SupportsTags,
    bool SupportsNonces,
    int MaxRecipients,
    int MaxAttachmentBytes);
