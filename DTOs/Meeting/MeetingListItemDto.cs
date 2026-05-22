namespace MeetingBackend.DTOs.Meeting;

public class MeetingListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string HostIdentity { get; set; } = string.Empty;
    public bool CanManagePoll { get; set; }

    /// <summary>User hiện tại là chủ trì gốc hoặc đồng chủ trì.</summary>
    public bool IsMeetingHost { get; set; }
    public string MeetingCode { get; set; } = string.Empty;
    public string Passcode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int ActiveParticipantCount { get; set; }
}
