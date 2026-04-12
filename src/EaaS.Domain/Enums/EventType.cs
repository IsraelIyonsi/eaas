namespace EaaS.Domain.Enums;

public enum EventType
{
    Queued,
    Scheduled,
    Sent,
    Delivered,
    Bounced,
    Complained,
    Opened,
    Clicked,
    Failed
}
