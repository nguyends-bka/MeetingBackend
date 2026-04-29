namespace MeetingBackend.DTOs.Meeting;

public class MeetingNotificationDto
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ActorUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? OpenedAt { get; set; }
}
