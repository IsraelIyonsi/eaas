namespace EaaS.Shared.Constants;

public static class WebhookConstants
{
    public const int MaxWebhooksPerTenant = 10;
    public const int TestTimeoutSeconds = 5;
    public const int DispatchTimeoutSeconds = 10;
    public const string SecretPrefix = "whsec_";

    /// <summary>
    /// After this many consecutive delivery failures the webhook is auto-disabled
    /// to prevent runaway retry cost against a dead endpoint (C3 rev-2).
    /// </summary>
    public const int AutoDisableThreshold = 50;
}
