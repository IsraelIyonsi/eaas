namespace EaaS.Domain.Enums;

public enum EventType
{
    Queued,
    Sent,
    Delivered,
    Bounced,
    Complained,
    Opened,
    Clicked,
    Failed
}
