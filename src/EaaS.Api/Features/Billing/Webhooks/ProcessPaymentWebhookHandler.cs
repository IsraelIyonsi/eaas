using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.Billing.Webhooks;

public sealed partial class ProcessPaymentWebhookHandler : IRequestHandler<ProcessPaymentWebhookCommand, Unit>
{
    private readonly AppDbContext _dbContext;
    private readonly IPaymentProviderFactory _paymentFactory;
    private readonly ILogger<ProcessPaymentWebhookHandler> _logger;

    public ProcessPaymentWebhookHandler(
        AppDbContext dbContext,
        IPaymentProviderFactory paymentFactory,
        ILogger<ProcessPaymentWebhookHandler> logger)
    {
        _dbContext = dbContext;
        _paymentFactory = paymentFactory;
        _logger = logger;
    }

    public async Task<Unit> Handle(ProcessPaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentProvider>(request.Provider, ignoreCase: true, out var providerEnum))
        {
            LogUnknownProvider(_logger, request.Provider);
            throw new ArgumentException($"Unknown payment provider: '{request.Provider}'.");
        }

        var provider = _paymentFactory.GetProvider(providerEnum);

        var webhookEvent = await provider.ParseWebhookAsync(request.Payload, request.Signature, cancellationToken);

        if (webhookEvent is null)
            throw new UnauthorizedAccessException("Invalid webhook signature.");

        LogWebhookReceived(_logger, webhookEvent.EventType, webhookEvent.ExternalId, request.Provider);

        switch (webhookEvent.EventType)
        {
            case "charge.success":
            case "invoice.payment_succeeded":
                await HandlePaymentSuccessAsync(webhookEvent, cancellationToken);
                break;

            case "subscription.create":
            case "customer.subscription.created":
                await HandleSubscriptionActivatedAsync(webhookEvent, cancellationToken);
                break;

            case "subscription.not_renew":
            case "customer.subscription.deleted":
                await HandleSubscriptionCancelledAsync(webhookEvent, cancellationToken);
                break;

            case "invoice.payment_failed":
                await HandlePaymentFailedAsync(webhookEvent, cancellationToken);
                break;

            default:
                LogUnknownEventType(_logger, webhookEvent.EventType, request.Provider);
                break;
        }

        return Unit.Value;
    }

    private async Task HandlePaymentSuccessAsync(WebhookEvent webhookEvent, CancellationToken ct)
    {
        var invoice = await _dbContext.Invoices
            .FirstOrDefaultAsync(i => i.ExternalPaymentId == webhookEvent.ExternalId, ct);

        if (invoice is null)
        {
            LogInvoiceNotFound(_logger, webhookEvent.ExternalId);
            return;
        }

        // Idempotent: skip if already paid
        if (invoice.Status == InvoiceStatus.Paid)
            return;

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        LogInvoiceMarkedPaid(_logger, invoice.Id, webhookEvent.ExternalId);
    }

    private async Task HandleSubscriptionActivatedAsync(WebhookEvent webhookEvent, CancellationToken ct)
    {
        var subscription = await _dbContext.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.ExternalSubscriptionId == webhookEvent.ExternalSubscriptionId, ct);

        if (subscription is null)
        {
            LogSubscriptionNotFound(_logger, webhookEvent.ExternalSubscriptionId ?? "null");
            return;
        }

        subscription.Status = SubscriptionStatus.Active;
        subscription.UpdatedAt = DateTime.UtcNow;

        // Sync plan limits to tenant
        if (subscription.Plan is not null)
        {
            var tenant = await _dbContext.Tenants.FindAsync(new object[] { subscription.TenantId }, ct);
            if (tenant is not null)
            {
                tenant.MonthlyEmailLimit = subscription.Plan.MonthlyEmailLimit;
                tenant.MaxApiKeys = subscription.Plan.MaxApiKeys;
                tenant.MaxDomainsCount = subscription.Plan.MaxDomains;
                tenant.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        LogSubscriptionActivated(_logger, subscription.Id, subscription.TenantId);
    }

    private async Task HandleSubscriptionCancelledAsync(WebhookEvent webhookEvent, CancellationToken ct)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.ExternalSubscriptionId == webhookEvent.ExternalSubscriptionId, ct);

        if (subscription is null)
        {
            LogSubscriptionNotFound(_logger, webhookEvent.ExternalSubscriptionId ?? "null");
            return;
        }

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        LogSubscriptionCancelled(_logger, subscription.Id, subscription.TenantId);
    }

    private async Task HandlePaymentFailedAsync(WebhookEvent webhookEvent, CancellationToken ct)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.ExternalSubscriptionId == webhookEvent.ExternalSubscriptionId, ct);

        if (subscription is null)
        {
            LogSubscriptionNotFound(_logger, webhookEvent.ExternalSubscriptionId ?? "null");
            return;
        }

        subscription.Status = SubscriptionStatus.PastDue;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        LogSubscriptionPastDue(_logger, subscription.Id, subscription.TenantId);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown payment provider in webhook: Provider={Provider}")]
    private static partial void LogUnknownProvider(ILogger logger, string provider);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook received: EventType={EventType}, ExternalId={ExternalId}, Provider={Provider}")]
    private static partial void LogWebhookReceived(ILogger logger, string eventType, string externalId, string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown webhook event type: EventType={EventType}, Provider={Provider}")]
    private static partial void LogUnknownEventType(ILogger logger, string eventType, string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invoice not found for ExternalPaymentId={ExternalPaymentId}")]
    private static partial void LogInvoiceNotFound(ILogger logger, string externalPaymentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Invoice marked as paid: InvoiceId={InvoiceId}, ExternalPaymentId={ExternalPaymentId}")]
    private static partial void LogInvoiceMarkedPaid(ILogger logger, Guid invoiceId, string externalPaymentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Subscription not found for ExternalSubscriptionId={ExternalSubscriptionId}")]
    private static partial void LogSubscriptionNotFound(ILogger logger, string externalSubscriptionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Subscription activated: SubscriptionId={SubscriptionId}, TenantId={TenantId}")]
    private static partial void LogSubscriptionActivated(ILogger logger, Guid subscriptionId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Subscription cancelled: SubscriptionId={SubscriptionId}, TenantId={TenantId}")]
    private static partial void LogSubscriptionCancelled(ILogger logger, Guid subscriptionId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Subscription past due: SubscriptionId={SubscriptionId}, TenantId={TenantId}")]
    private static partial void LogSubscriptionPastDue(ILogger logger, Guid subscriptionId, Guid tenantId);
}
