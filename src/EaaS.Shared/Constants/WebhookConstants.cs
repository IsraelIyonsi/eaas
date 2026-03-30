namespace EaaS.Shared.Constants;

public static class WebhookConstants
{
    public const int MaxWebhooksPerTenant = 10;
    public const int TestTimeoutSeconds = 5;
    public const int DispatchTimeoutSeconds = 10;
    public const string SecretPrefix = "whsec_";
}
