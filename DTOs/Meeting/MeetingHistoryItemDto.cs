namespace MeetingBackend.DTOs.Meeting;

public class MeetingHistoryItemDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public double? Duration { get; set; }
}
