namespace EaaS.Domain.Enums;

/// <summary>
/// Terminal/intermediate states for a single <c>(webhook, email, event_type)</c>
/// delivery tuple. Used by <see cref="EaaS.Domain.Entities.WebhookDelivery"/> to
/// short-circuit duplicate dispatches on MassTransit consumer retries (H11).
/// </summary>
public enum WebhookDeliveryStatus
{
    Pending,
    Succeeded,
    Failed
}
