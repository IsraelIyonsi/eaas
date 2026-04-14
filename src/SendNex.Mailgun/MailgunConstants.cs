namespace SendNex.Mailgun;

/// <summary>
/// Zero-magic-strings home for every Mailgun literal consumed by the client,
/// adapter, verifier, and normalizer. If a new literal is needed, add it here
/// first — do not inline string literals anywhere else.
/// </summary>
public static class MailgunConstants
{
    /// <summary>Default Mailgun API base URL (US region).</summary>
    public const string DefaultApiBaseUrl = "https://api.mailgun.net";

    /// <summary>EU region API base URL.</summary>
    public const string EuApiBaseUrl = "https://api.eu.mailgun.net";

    /// <summary>Named <see cref="System.Net.Http.HttpClient"/> key used by the typed client.</summary>
    public const string HttpClientName = "SendNex.Mailgun";

    /// <summary>HTTP Basic-auth username for Mailgun (password is the API key).</summary>
    public const string BasicAuthUser = "api";

    public static class Regions
    {
        public const string Us = "US";
        public const string Eu = "EU";
    }

    /// <summary>URL path segments for the Mailgun v3 REST API.</summary>
    public static class Paths
    {
        public const string ApiVersionSegment = "/v3/";
        public const string MessagesSegment = "/messages";
        public const string MessagesMimeSegment = "/messages.mime";
    }

    /// <summary>Form field keys used on <c>POST /v3/{domain}/messages</c>.</summary>
    public static class FormFields
    {
        public const string From = "from";
        public const string To = "to";
        public const string Cc = "cc";
        public const string Bcc = "bcc";
        public const string Subject = "subject";
        public const string Text = "text";
        public const string Html = "html";
        public const string Tag = "o:tag";
        public const string TrackingOpens = "o:tracking-opens";
        public const string TrackingClicks = "o:tracking-clicks";
        public const string HeaderPrefix = "h:";
        public const string VariablePrefix = "v:";
        public const string Attachment = "attachment";
        public const string Message = "message";
    }

    /// <summary>Well-known Mailgun custom-variable keys set by the adapter.</summary>
    public static class CustomVariables
    {
        public const string TenantId = "tenant_id";
    }

    /// <summary>Webhook payload field names (flat or nested under <c>signature</c>).</summary>
    public static class Webhook
    {
        public const string SignatureObject = "signature";
        public const string Timestamp = "timestamp";
        public const string Token = "token";
        public const string Signature = "signature";
        public const string EventData = "event-data";
        public const string Event = "event";
        public const string UserVariables = "user-variables";
        public const string Recipient = "recipient";
        public const string Severity = "severity";
        public const string Reason = "reason";
        public const string Message = "message";
        public const string Headers = "headers";
        public const string MessageId = "message-id";
        public const string DeliveryStatus = "delivery-status";
        public const string Code = "code";

        /// <summary>Replay tolerance for webhook timestamps (Mailgun recommends &lt;= 5 min).</summary>
        public static readonly System.TimeSpan ReplayTolerance = System.TimeSpan.FromMinutes(5);
    }

    /// <summary>Mailgun outbound event-type literals emitted on the webhook.</summary>
    public static class Events
    {
        public const string Accepted = "accepted";
        public const string Rejected = "rejected";
        public const string Delivered = "delivered";
        public const string Failed = "failed";
        public const string Opened = "opened";
        public const string Clicked = "clicked";
        public const string Unsubscribed = "unsubscribed";
        public const string Complained = "complained";
        public const string Stored = "stored";
    }

    /// <summary>Severity values attached to <c>failed</c> events.</summary>
    public static class Severity
    {
        public const string Permanent = "permanent";
        public const string Temporary = "temporary";
    }
}
