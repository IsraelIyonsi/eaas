namespace EaaS.Api.Constants;

public static class RouteConstants
{
    private const string ApiBase = "/api/v1";

    public const string Keys = $"{ApiBase}/keys";
    public const string Domains = $"{ApiBase}/domains";
    public const string Emails = $"{ApiBase}/emails";
    public const string Templates = $"{ApiBase}/templates";
    public const string Suppressions = $"{ApiBase}/suppressions";
    public const string Analytics = $"{ApiBase}/analytics";
    public const string Webhooks = $"{ApiBase}/webhooks";
    public const string InboundRules = $"{ApiBase}/inbound/rules";
    public const string InboundEmails = $"{ApiBase}/inbound/emails";

    // Customer auth routes
    public const string CustomerAuth = $"{ApiBase}/auth";

    // Admin routes
    private const string AdminBase = $"{ApiBase}/admin";
    public const string AdminAuth = $"{AdminBase}/auth";
    public const string AdminTenants = $"{AdminBase}/tenants";
    public const string AdminUsers = $"{AdminBase}/users";
    public const string AdminHealth = $"{AdminBase}/health";
    public const string AdminAnalytics = $"{AdminBase}/analytics";
    public const string AdminAuditLogs = $"{AdminBase}/audit-logs";
    public const string AdminBillingPlans = $"{AdminBase}/billing/plans";
    public const string BillingSubscriptions = $"{ApiBase}/billing/subscriptions";
    public const string PaymentWebhooks = $"{ApiBase}/webhooks/payments";
}
