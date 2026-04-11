namespace EaaS.Infrastructure.Payments;

/// <summary>
/// Configuration for payment providers. Bind from appsettings.json "Payments" section.
/// Only providers with non-empty keys are registered in the factory.
/// </summary>
public sealed class PaymentSettings
{
    public StripeSettings? Stripe { get; set; }
    public PayStackSettings? PayStack { get; set; }
    public FlutterwaveSettings? Flutterwave { get; set; }
    public PayPalSettings? PayPal { get; set; }
}

public sealed class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

public sealed class PayStackSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}

public sealed class FlutterwaveSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty;
}

public sealed class PayPalSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string WebhookId { get; set; } = string.Empty;
    public bool UseSandbox { get; set; } = true;
}
