using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class EmailEvent
{
    public Guid Id { get; set; }
    public Guid EmailId { get; set; }
    public EventType EventType { get; set; }
    public string Data { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Email Email { get; set; } = null!;
}
