namespace ADSUS_BE.BLL.Common.DTOs;

public class NotificationMessage
{
    public Guid LogId { get; set; }
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Body { get; set; }
    public string? DeepLink { get; set; }
    public DateTime SentAt { get; set; }
}
