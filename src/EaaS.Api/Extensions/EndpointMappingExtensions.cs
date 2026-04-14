using EaaS.Api.Constants;
using EaaS.Api.Features.Admin.Analytics;
using EaaS.Api.Features.Admin.AuditLogs;
using EaaS.Api.Features.Admin.Auth;
using EaaS.Api.Features.Admin.Health;
using EaaS.Api.Features.Admin.Tenants;
using EaaS.Api.Features.Admin.Users;
using EaaS.Api.Features.Analytics;
using EaaS.Api.Features.ApiKeys;
using EaaS.Api.Features.CustomerAuth;
using EaaS.Api.Features.Domains;
using EaaS.Api.Features.Emails;
using EaaS.Api.Features.Suppressions;
using EaaS.Api.Features.Templates;
using EaaS.Api.Features.Unsubscribe;
using EaaS.Api.Features.Inbound.Emails;
using EaaS.Api.Features.Inbound.Rules;
using EaaS.Api.Features.Inbound.Simulate;
using EaaS.Api.Features.Billing.Plans;
using EaaS.Api.Features.Billing.Subscriptions;
using EaaS.Api.Features.Billing.Webhooks;
using EaaS.Api.Features.Webhooks;

namespace EaaS.Api.Extensions;

public static class EndpointMappingExtensions
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        // API Key endpoints
        var keysGroup = app.MapGroup(RouteConstants.Keys)
            .RequireAuthorization()
            .WithTags(TagConstants.ApiKeys);

        CreateApiKeyEndpoint.Map(keysGroup);
        RevokeApiKeyEndpoint.Map(keysGroup);
        ListApiKeysEndpoint.Map(keysGroup);
        RotateApiKeyEndpoint.Map(keysGroup);

        // Domain endpoints
        var domainsGroup = app.MapGroup(RouteConstants.Domains)
            .RequireAuthorization()
            .WithTags(TagConstants.Domains);

        AddDomainEndpoint.Map(domainsGroup);
        ListDomainsEndpoint.Map(domainsGroup);
        GetDomainEndpoint.Map(domainsGroup);
        VerifyDomainEndpoint.Map(domainsGroup);
        RemoveDomainEndpoint.Map(domainsGroup);

        // Email endpoints
        var emailsGroup = app.MapGroup(RouteConstants.Emails)
            .RequireAuthorization()
            .WithTags(TagConstants.Emails);

        SendEmailEndpoint.Map(emailsGroup);
        SendBatchEndpoint.Map(emailsGroup);
        ScheduleEmailEndpoint.Map(emailsGroup);
        GetEmailEndpoint.Map(emailsGroup);
        GetEmailEventsEndpoint.Map(emailsGroup);
        ListEmailsEndpoint.Map(emailsGroup);

        // Template endpoints
        var templatesGroup = app.MapGroup(RouteConstants.Templates)
            .RequireAuthorization()
            .WithTags(TagConstants.Templates);

        CreateTemplateEndpoint.Map(templatesGroup);
        GetTemplateEndpoint.Map(templatesGroup);
        ListTemplatesEndpoint.Map(templatesGroup);
        UpdateTemplateEndpoint.Map(templatesGroup);
        DeleteTemplateEndpoint.Map(templatesGroup);
        PreviewTemplateEndpoint.Map(templatesGroup);
        ListTemplateVersionsEndpoint.Map(templatesGroup);
        RollbackTemplateEndpoint.Map(templatesGroup);

        // Suppression endpoints
        var suppressionsGroup = app.MapGroup(RouteConstants.Suppressions)
            .RequireAuthorization()
            .WithTags(TagConstants.Suppressions);

        ListSuppressionsEndpoint.Map(suppressionsGroup);
        AddSuppressionEndpoint.Map(suppressionsGroup);
        RemoveSuppressionEndpoint.Map(suppressionsGroup);

        // Analytics endpoints
        var analyticsGroup = app.MapGroup(RouteConstants.Analytics)
            .RequireAuthorization()
            .WithTags(TagConstants.Analytics);

        GetAnalyticsSummaryEndpoint.Map(analyticsGroup);
        GetAnalyticsTimelineEndpoint.Map(analyticsGroup);
        InboundAnalyticsEndpoints.MapInboundAnalytics(analyticsGroup);

        // Webhook endpoints
        var webhooksGroup = app.MapGroup(RouteConstants.Webhooks)
            .RequireAuthorization()
            .WithTags(TagConstants.Webhooks);

        CreateWebhookEndpoint.Map(webhooksGroup);
        ListWebhooksEndpoint.Map(webhooksGroup);
        GetWebhookEndpoint.Map(webhooksGroup);
        GetWebhookDeliveriesEndpoint.Map(webhooksGroup);
        UpdateWebhookEndpoint.Map(webhooksGroup);
        DeleteWebhookEndpoint.Map(webhooksGroup);
        TestWebhookEndpoint.Map(webhooksGroup);

        // Inbound Rules endpoints
        var inboundRulesGroup = app.MapGroup(RouteConstants.InboundRules)
            .RequireAuthorization()
            .WithTags(TagConstants.InboundRules);

        CreateInboundRuleEndpoint.Map(inboundRulesGroup);
        ListInboundRulesEndpoint.Map(inboundRulesGroup);
        GetInboundRuleEndpoint.Map(inboundRulesGroup);
        UpdateInboundRuleEndpoint.Map(inboundRulesGroup);
        DeleteInboundRuleEndpoint.Map(inboundRulesGroup);

        // Inbound Emails endpoints
        var inboundEmailsGroup = app.MapGroup(RouteConstants.InboundEmails)
            .RequireAuthorization()
            .WithTags(TagConstants.InboundEmails);

        ListInboundEmailsEndpoint.Map(inboundEmailsGroup);
        GetInboundEmailEndpoint.Map(inboundEmailsGroup);
        DeleteInboundEmailEndpoint.Map(inboundEmailsGroup);
        RetryInboundWebhookEndpoint.Map(inboundEmailsGroup);

        // Inbound simulation (development only)
        if (app.Environment.IsDevelopment())
        {
            SimulateInboundEndpoint.Map(inboundEmailsGroup);
        }

        // Customer auth endpoints (no authorization required — this is registration/login)
        var customerAuthGroup = app.MapGroup(RouteConstants.CustomerAuth)
            .RequireRateLimiting("AuthLogin")
            .WithTags(TagConstants.CustomerAuth);

        RegisterEndpoint.Map(customerAuthGroup);
        CustomerLoginEndpoint.Map(customerAuthGroup);
        CustomerLogoutEndpoint.Map(customerAuthGroup);

        // Admin auth endpoints (no AdminPolicy — this IS the login)
        var adminAuthGroup = app.MapGroup(RouteConstants.AdminAuth)
            .RequireRateLimiting("AuthLogin")
            .WithTags(TagConstants.AdminAuth);

        AdminLoginEndpoint.Map(adminAuthGroup);

        // Admin tenant endpoints (Admin + SuperAdmin)
        var adminTenantsGroup = app.MapGroup(RouteConstants.AdminTenants)
            .RequireAuthorization(AuthorizationPolicyConstants.AdminPolicy)
            .WithTags(TagConstants.AdminTenants);

        ListTenantsEndpoint.Map(adminTenantsGroup);
        GetTenantEndpoint.Map(adminTenantsGroup);
        CreateTenantEndpoint.Map(adminTenantsGroup);
        UpdateTenantEndpoint.Map(adminTenantsGroup);
        SuspendTenantEndpoint.Map(adminTenantsGroup);
        ActivateTenantEndpoint.Map(adminTenantsGroup);
        DeleteTenantEndpoint.Map(adminTenantsGroup);

        // Admin users endpoints (SuperAdmin only)
        var adminUsersGroup = app.MapGroup(RouteConstants.AdminUsers)
            .RequireAuthorization(AuthorizationPolicyConstants.SuperAdminPolicy)
            .WithTags(TagConstants.AdminUsers);

        ListAdminUsersEndpoint.Map(adminUsersGroup);
        CreateAdminUserEndpoint.Map(adminUsersGroup);
        UpdateAdminUserEndpoint.Map(adminUsersGroup);
        DeleteAdminUserEndpoint.Map(adminUsersGroup);

        // Admin health endpoints (all admin roles)
        var adminHealthGroup = app.MapGroup(RouteConstants.AdminHealth)
            .RequireAuthorization(AuthorizationPolicyConstants.AdminReadPolicy)
            .WithTags(TagConstants.AdminHealth);

        GetSystemHealthEndpoint.Map(adminHealthGroup);

        // Admin analytics endpoints (Admin + SuperAdmin)
        var adminAnalyticsGroup = app.MapGroup(RouteConstants.AdminAnalytics)
            .RequireAuthorization(AuthorizationPolicyConstants.AdminPolicy)
            .WithTags(TagConstants.AdminAnalytics);

        GetPlatformSummaryEndpoint.Map(adminAnalyticsGroup);
        GetPlatformTimelineEndpoint.Map(adminAnalyticsGroup);
        GetTenantRankingsEndpoint.Map(adminAnalyticsGroup);
        GetGrowthMetricsEndpoint.Map(adminAnalyticsGroup);

        // Admin audit log endpoints (all admin roles)
        var adminAuditLogsGroup = app.MapGroup(RouteConstants.AdminAuditLogs)
            .RequireAuthorization(AuthorizationPolicyConstants.AdminReadPolicy)
            .WithTags(TagConstants.AdminAuditLogs);

        ListAuditLogsEndpoint.Map(adminAuditLogsGroup);

        // Admin billing plans endpoints (SuperAdmin only)
        var adminBillingPlansGroup = app.MapGroup(RouteConstants.AdminBillingPlans)
            .RequireAuthorization(AuthorizationPolicyConstants.SuperAdminPolicy)
            .WithTags(TagConstants.BillingPlans);

        ListPlansEndpoint.Map(adminBillingPlansGroup);
        GetPlanEndpoint.Map(adminBillingPlansGroup);
        CreatePlanEndpoint.Map(adminBillingPlansGroup);
        UpdatePlanEndpoint.Map(adminBillingPlansGroup);

        // Customer billing plans (public, authenticated — no admin policy)
        var billingPlansGroup = app.MapGroup(RouteConstants.BillingPlans)
            .RequireAuthorization()
            .WithTags(TagConstants.BillingPlans);

        ListBillingPlansEndpoint.Map(billingPlansGroup);

        // Billing subscription endpoints (customer-facing)
        var billingGroup = app.MapGroup(RouteConstants.BillingSubscriptions)
            .RequireAuthorization()
            .WithTags(TagConstants.BillingSubscriptions);

        GetSubscriptionEndpoint.Map(billingGroup);
        CreateSubscriptionEndpoint.Map(billingGroup);
        CancelSubscriptionEndpoint.Map(billingGroup);
        ListInvoicesEndpoint.Map(billingGroup);

        // Payment webhook endpoints (anonymous - called by payment providers)
        var paymentWebhooksGroup = app.MapGroup(RouteConstants.PaymentWebhooks)
            .WithTags(TagConstants.PaymentWebhooks);

        ProcessPaymentWebhookEndpoint.Map(paymentWebhooksGroup);

        // List-Unsubscribe endpoints (CAN-SPAM §7704(a)(4) + RFC 8058) — top-level, anonymous
        UnsubscribeEndpoint.Map(app);

        return app;
    }
}
