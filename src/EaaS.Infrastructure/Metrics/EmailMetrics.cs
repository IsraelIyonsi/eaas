using Prometheus;

namespace EaaS.Infrastructure.Metrics;

public static class EmailMetrics
{
    public static readonly Counter EmailsSent = Prometheus.Metrics.CreateCounter(
        "eaas_emails_sent_total", "Total emails sent",
        new CounterConfiguration { LabelNames = new[] { "tenant_id", "status" } });

    public static readonly Counter EmailsReceived = Prometheus.Metrics.CreateCounter(
        "eaas_emails_received_total", "Total inbound emails received",
        new CounterConfiguration { LabelNames = new[] { "tenant_id", "status" } });

    public static readonly Histogram EmailProcessingDuration = Prometheus.Metrics.CreateHistogram(
        "eaas_email_processing_seconds", "Email processing duration",
        new HistogramConfiguration { LabelNames = new[] { "type" }, Buckets = Histogram.ExponentialBuckets(0.01, 2, 10) });

    public static readonly Counter WebhookDispatched = Prometheus.Metrics.CreateCounter(
        "eaas_webhooks_dispatched_total", "Total webhooks dispatched",
        new CounterConfiguration { LabelNames = new[] { "status" } });

    public static readonly Gauge QueueDepth = Prometheus.Metrics.CreateGauge(
        "eaas_queue_depth", "Current queue depth",
        new GaugeConfiguration { LabelNames = new[] { "queue" } });

    public static readonly Histogram WebhookDispatchDuration = Prometheus.Metrics.CreateHistogram(
        "eaas_webhook_dispatch_seconds", "Webhook dispatch duration",
        new HistogramConfiguration { LabelNames = new[] { "tenant_id" }, Buckets = Histogram.ExponentialBuckets(0.01, 2, 10) });

    public static readonly Counter BouncesTotal = Prometheus.Metrics.CreateCounter(
        "eaas_bounces_total", "Total bounces processed",
        new CounterConfiguration { LabelNames = new[] { "tenant_id", "bounce_type" } });

    public static readonly Counter ComplaintsTotal = Prometheus.Metrics.CreateCounter(
        "eaas_complaints_total", "Total complaints processed",
        new CounterConfiguration { LabelNames = new[] { "tenant_id" } });

    public static readonly Counter TemplateRendersTotal = Prometheus.Metrics.CreateCounter(
        "eaas_template_renders_total", "Total template renders",
        new CounterConfiguration { LabelNames = new[] { "tenant_id", "status" } });

    public static readonly Counter RateLimitExceeded = Prometheus.Metrics.CreateCounter(
        "eaas_rate_limit_exceeded_total", "Total rate limit exceeded events",
        new CounterConfiguration { LabelNames = new[] { "tenant_id" } });

    public static readonly Counter AdminOperationsTotal = Prometheus.Metrics.CreateCounter(
        "eaas_admin_operations_total", "Total admin operations",
        new CounterConfiguration { LabelNames = new[] { "operation" } });
}
