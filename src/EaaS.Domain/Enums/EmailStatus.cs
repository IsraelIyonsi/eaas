namespace EaaS.Domain.Enums;

public enum EmailStatus
{
    Queued,
    Sending,
    Sent,
    Delivered,
    Bounced,
    Complained,
    Failed,
    Scheduled
}
