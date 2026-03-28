namespace EaaS.Domain.Entities;

public class TrackingLink
{
    public Guid Id { get; set; }
    public Guid EmailId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public DateTime? ClickedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Email Email { get; set; } = null!;
}
