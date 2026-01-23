namespace MeetingBackend.DTOs.Meeting;

public class MyHistoryItemDto
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public double? Duration { get; set; }
    public string MeetingCode { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
}
