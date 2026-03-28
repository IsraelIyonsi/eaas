using System.Text.Json.Serialization;

namespace EaaS.WebhookProcessor.Models;

public sealed class SnsMessage
{
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("MessageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("Token")]
    public string? Token { get; set; }

    [JsonPropertyName("TopicArn")]
    public string? TopicArn { get; set; }

    [JsonPropertyName("Subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("Timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("SignatureVersion")]
    public string? SignatureVersion { get; set; }

    [JsonPropertyName("Signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("SigningCertURL")]
    public string? SigningCertUrl { get; set; }

    [JsonPropertyName("SubscribeURL")]
    public string? SubscribeUrl { get; set; }

    [JsonPropertyName("UnsubscribeURL")]
    public string? UnsubscribeUrl { get; set; }
}

public sealed class SesNotification
{
    [JsonPropertyName("notificationType")]
    public string NotificationType { get; set; } = string.Empty;

    [JsonPropertyName("mail")]
    public SesMail Mail { get; set; } = new();

    [JsonPropertyName("bounce")]
    public SesBounce? Bounce { get; set; }

    [JsonPropertyName("complaint")]
    public SesComplaint? Complaint { get; set; }

    [JsonPropertyName("delivery")]
    public SesDelivery? Delivery { get; set; }
}

public sealed class SesMail
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("destination")]
    public List<string> Destination { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

public sealed class SesBounce
{
    [JsonPropertyName("bounceType")]
    public string BounceType { get; set; } = string.Empty;

    [JsonPropertyName("bounceSubType")]
    public string BounceSubType { get; set; } = string.Empty;

    [JsonPropertyName("bouncedRecipients")]
    public List<SesBouncedRecipient> BouncedRecipients { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("feedbackId")]
    public string? FeedbackId { get; set; }
}

public sealed class SesBouncedRecipient
{
    [JsonPropertyName("emailAddress")]
    public string EmailAddress { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("diagnosticCode")]
    public string? DiagnosticCode { get; set; }
}

public sealed class SesComplaint
{
    [JsonPropertyName("complainedRecipients")]
    public List<SesComplainedRecipient> ComplainedRecipients { get; set; } = new();

    [JsonPropertyName("complaintFeedbackType")]
    public string? ComplaintFeedbackType { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("feedbackId")]
    public string? FeedbackId { get; set; }
}

public sealed class SesComplainedRecipient
{
    [JsonPropertyName("emailAddress")]
    public string EmailAddress { get; set; } = string.Empty;
}

public sealed class SesDelivery
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("recipients")]
    public List<string> Recipients { get; set; } = new();

    [JsonPropertyName("processingTimeMillis")]
    public int? ProcessingTimeMillis { get; set; }

    [JsonPropertyName("smtpResponse")]
    public string? SmtpResponse { get; set; }
}
